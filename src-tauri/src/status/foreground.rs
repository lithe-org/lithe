use std::ffi::c_void;
use std::os::windows::ffi::OsStrExt;
use windows_sys::Win32::Foundation::{CloseHandle, HANDLE, HWND};
use windows_sys::Win32::Storage::FileSystem::{
    GetFileVersionInfoSizeW, GetFileVersionInfoW, VerQueryValueW,
};
use windows_sys::Win32::System::Threading::{
    OpenProcess, QueryFullProcessImageNameW, PROCESS_QUERY_LIMITED_INFORMATION,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    GetClassNameW, GetForegroundWindow, GetWindowTextW, GetWindowThreadProcessId,
};

use crate::commands::system::ForegroundApp;

const TITLE_BUFFER_CHARS: usize = 512;
const PATH_BUFFER_CHARS: usize = 32768;
const CLASS_BUFFER_CHARS: usize = 64;

// PRD 5.2.2：浏览器标题是网页标题，必须回落到应用自身的名称。
const BROWSER_NAMES: &[(&str, &str)] = &[
    ("chrome", "Google Chrome"),
    ("msedge", "Microsoft Edge"),
    ("firefox", "Mozilla Firefox"),
    ("brave", "Brave Browser"),
    ("opera", "Opera"),
];

const TITLE_SEPARATORS: &[&str] = &[" - ", " — ", " | ", " – "];

#[derive(Default, Debug, Clone)]
pub struct ForegroundSnapshot {
    pub title: String,
    pub exe_path: String,
    pub description: Option<String>,
}

/// 桌面与任务栏窗口类都属于 explorer.exe，按普通进程读会显示「Windows 文件资源管理器」。
pub fn desktop_name(class_name: &str) -> Option<&'static str> {
    match class_name {
        "Progman" | "WorkerW" => Some("桌面"),
        "Shell_TrayWnd" | "Shell_SecondaryTrayWnd" => Some("任务栏"),
        _ => None,
    }
}

/// shell 窗口铺满屏幕但不是全屏应用，必须排除，否则会误隐藏顶栏。
pub fn is_shell_window_class(class_name: &str) -> bool {
    desktop_name(class_name).is_some()
}

pub fn capture_app() -> ForegroundApp {
    let hwnd: HWND = unsafe { GetForegroundWindow() };
    if hwnd.is_null() {
        return ForegroundApp {
            name: "应用".to_string(),
            executable: String::new(),
            controllable: false,
        };
    }
    if let Some(name) = desktop_name(&window_class(hwnd)) {
        return ForegroundApp {
            name: name.to_string(),
            executable: String::new(),
            controllable: false,
        };
    }
    app_for_window(hwnd)
}

/// 供窗口控制命令复用：把任意窗口描述成前端契约。
pub fn app_for_window(hwnd: HWND) -> ForegroundApp {
    let snapshot = snapshot_for(hwnd);
    ForegroundApp {
        name: resolve_name(&snapshot),
        executable: exe_file_name(&snapshot.exe_path),
        controllable: is_controllable_window(hwnd),
    }
}

/// 桌面、任务栏与 Lithe 自身不能作为窗口控制对象（PRD 5.3.2）。
pub fn is_controllable_window(hwnd: HWND) -> bool {
    if hwnd.is_null() {
        return false;
    }
    let class = window_class(hwnd);
    if is_shell_window_class(&class) {
        return false;
    }
    let pid = window_process_id(hwnd);
    pid != 0 && pid != std::process::id()
}

pub fn window_process_id(hwnd: HWND) -> u32 {
    let mut pid = 0u32;
    unsafe { GetWindowThreadProcessId(hwnd, &mut pid) };
    pid
}

/// 纯逻辑，按 PRD 5.2.2 的优先级解析；与 Win32 调用分离后才能脱离桌面环境做单测。
pub fn resolve_name(snapshot: &ForegroundSnapshot) -> String {
    if let Some(description) = cleaned(snapshot.description.as_deref()) {
        return description;
    }
    // 浏览器窗口标题是网页标题，按分隔符取后缀会拿到页面名而不是应用名。
    if let Some(browser) = browser_app_name(&snapshot.exe_path) {
        return browser.to_string();
    }
    if let Some(suffix) = split_title_suffix(&snapshot.title) {
        return suffix;
    }
    if let Some(stem) = exe_stem(&snapshot.exe_path) {
        return stem;
    }
    "应用".to_string()
}

fn cleaned(value: Option<&str>) -> Option<String> {
    let text = value?.trim();
    (!text.is_empty()).then(|| text.to_string())
}

pub fn browser_app_name(exe_path: &str) -> Option<&'static str> {
    let stem = exe_stem(exe_path)?.to_ascii_lowercase();
    BROWSER_NAMES
        .iter()
        .find(|(name, _)| stem.as_str() == *name)
        .map(|(_, label)| *label)
}

pub fn split_title_suffix(title: &str) -> Option<String> {
    let title = title.trim();
    // 取最靠右的分隔符：VS Code 这类标题有多段后缀，最后一段才是应用名。
    let (position, separator) = TITLE_SEPARATORS
        .iter()
        .filter_map(|sep| title.rfind(sep).map(|pos| (pos, *sep)))
        .max_by_key(|(pos, _)| *pos)?;
    cleaned(Some(&title[position + separator.len()..]))
}

pub fn exe_stem(path: &str) -> Option<String> {
    let file = exe_file_name(path);
    let stem = file.rsplit_once('.').map_or(file.as_str(), |(stem, _)| stem).trim();
    cleaned(Some(stem))
}

pub fn exe_file_name(path: &str) -> String {
    path.rsplit(['\\', '/']).next().unwrap_or("").trim().to_string()
}

pub fn capture_snapshot() -> ForegroundSnapshot {
    let hwnd: HWND = unsafe { GetForegroundWindow() };
    if hwnd.is_null() {
        return ForegroundSnapshot::default();
    }
    snapshot_for(hwnd)
}

fn snapshot_for(hwnd: HWND) -> ForegroundSnapshot {
    let title = read_window_title(hwnd);
    let exe_path = read_process_path(hwnd);
    let description = read_file_description(&exe_path);
    ForegroundSnapshot {
        title,
        exe_path,
        description,
    }
}

pub fn window_class(hwnd: HWND) -> String {
    // FFI 边界收在显式 unsafe 块内（edition 2024）。
    unsafe {
        let mut buffer = [0u16; CLASS_BUFFER_CHARS];
        let length = GetClassNameW(hwnd, buffer.as_mut_ptr(), buffer.len() as i32);
        if length <= 0 {
            return String::new();
        }
        String::from_utf16_lossy(&buffer[..(length as usize).min(buffer.len())])
    }
}

fn read_window_title(hwnd: HWND) -> String {
    // Win32 FFI 边界收在一个显式 unsafe 块内，符合 edition 2024 的要求。
    unsafe {
        let mut buffer = [0u16; TITLE_BUFFER_CHARS];
        let length = GetWindowTextW(hwnd, buffer.as_mut_ptr(), buffer.len() as i32);
        if length <= 0 {
            return String::new();
        }
        // from_utf16_lossy 按字符切片，不会像按字节 truncate 那样切在代理对中间（坑 #17）。
        String::from_utf16_lossy(&buffer[..(length as usize).min(buffer.len())])
    }
}

fn read_process_path(hwnd: HWND) -> String {
    let pid = window_process_id(hwnd);
    if pid == 0 {
        return String::new();
    }
    unsafe {
        let process: HANDLE = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid);
        if process.is_null() {
            return String::new();
        }
        let mut buffer = [0u16; PATH_BUFFER_CHARS];
        let mut size = buffer.len() as u32;
        let ok = QueryFullProcessImageNameW(process, 0, buffer.as_mut_ptr(), &mut size);
        CloseHandle(process);
        if ok == 0 {
            return String::new();
        }
        String::from_utf16_lossy(&buffer[..(size as usize).min(buffer.len())])
    }
}

fn read_file_description(path: &str) -> Option<String> {
    unsafe {
        if path.is_empty() {
            return None;
        }
        let mut wide: Vec<u16> = std::ffi::OsStr::new(path).encode_wide().collect();
        wide.push(0);
        let mut handle = 0u32;
        let size = GetFileVersionInfoSizeW(wide.as_ptr(), &mut handle);
        if size == 0 {
            return None;
        }
        let mut block = vec![0u8; size as usize];
        // dwhandle 在 MSDN 中标记为忽略，传 0 即可；size 才是缓冲区字节数。
        if GetFileVersionInfoW(wide.as_ptr(), 0, size, block.as_mut_ptr() as *mut c_void) == 0 {
            return None;
        }

        #[repr(C)]
        #[derive(Clone, Copy)]
        struct Translation {
            language: u16,
            code_page: u16,
        }

        let mut table_ptr: *mut c_void = std::ptr::null_mut();
        let mut table_len = 0u32;
        let translation_key = wide_string("\\VarFileInfo\\Translation");
        if VerQueryValueW(
            block.as_ptr() as *const c_void,
            translation_key.as_ptr(),
            &mut table_ptr,
            &mut table_len,
        ) == 0
            || table_len < 4
            || table_ptr.is_null()
        {
            return None;
        }
        let translations = std::slice::from_raw_parts(
            table_ptr as *const Translation,
            (table_len / 4) as usize,
        );

        for entry in translations {
            let key = wide_string(&format!(
                "\\StringFileInfo\\{:04x}{:04x}\\FileDescription",
                entry.language, entry.code_page
            ));
            let mut value_ptr: *mut c_void = std::ptr::null_mut();
            let mut value_len = 0u32;
            if VerQueryValueW(
                block.as_ptr() as *const c_void,
                key.as_ptr(),
                &mut value_ptr,
                &mut value_len,
            ) == 0
                || value_ptr.is_null()
            {
                continue;
            }
            // 字符串值的长度单位是 WCHAR，含结尾 null；接收 trim 的返回值（坑 #16）。
            let chars = std::slice::from_raw_parts(value_ptr as *const u16, value_len as usize);
            let text = String::from_utf16_lossy(chars)
                .trim_end_matches('\0')
                .trim()
                .to_string();
            if !text.is_empty() {
                return Some(text);
            }
        }
        None
    }
}

fn wide_string(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(std::iter::once(0)).collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn snapshot(description: Option<&str>, title: &str, exe_path: &str) -> ForegroundSnapshot {
        ForegroundSnapshot {
            title: title.to_string(),
            exe_path: exe_path.to_string(),
            description: description.map(str::to_string),
        }
    }

    const VSCODE: &str = r"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\Code.exe";
    const CHROME: &str = r"C:\Program Files\Google\Chrome\Application\chrome.exe";

    #[test]
    fn description_has_highest_priority() {
        // 版本资源是应用自报的名称，比标题和 exe 名都可信。
        let app = snapshot(Some("Visual Studio Code"), "文档 - 记事本", VSCODE);
        assert_eq!(resolve_name(&app), "Visual Studio Code");
    }

    #[test]
    fn title_suffix_is_used_when_description_is_missing() {
        // 多段标题要取最右一段，否则会拿到文档名。
        let app = snapshot(None, "● App.tsx - lithe - Visual Studio Code", VSCODE);
        assert_eq!(resolve_name(&app), "Visual Studio Code");

        for separator in [" - ", " — ", " | ", " – "] {
            let title = format!("文档{separator}某应用");
            let app = snapshot(None, &title, VSCODE);
            assert_eq!(resolve_name(&app), "某应用", "分隔符 {separator:?}");
        }
    }

    #[test]
    fn exe_stem_is_used_when_title_has_no_separator() {
        let app = snapshot(None, "无标题", r"C:\Windows\System32\notepad.exe");
        assert_eq!(resolve_name(&app), "notepad");
    }

    #[test]
    fn placeholder_is_used_when_every_source_is_empty() {
        let app = snapshot(Some("  "), "", "");
        assert_eq!(resolve_name(&app), "应用");
    }

    #[test]
    fn null_window_cannot_be_controlled() {
        // 空句柄必须提前拒绝，否则后续读取窗口样式会拿到随机值。
        assert!(!is_controllable_window(std::ptr::null_mut()));
    }

    #[test]
    fn desktop_classes_are_shown_as_desktop_not_explorer() {
        // 桌面窗口属于 explorer.exe，按普通进程读会显示「Windows 文件资源管理器」。
        assert_eq!(desktop_name("Progman"), Some("桌面"));
        assert_eq!(desktop_name("WorkerW"), Some("桌面"));
        assert_eq!(desktop_name("Shell_TrayWnd"), Some("任务栏"));
        assert_eq!(desktop_name("Shell_SecondaryTrayWnd"), Some("任务栏"));
        assert_eq!(desktop_name("Chrome_WidgetWin_1"), None);
        assert!(is_shell_window_class("Shell_TrayWnd"));
        assert!(!is_shell_window_class("Chrome_WidgetWin_1"));
    }

    #[test]
    fn browser_shows_app_name_instead_of_web_page_title() {
        // 网页标题里带分隔符时不该把 "Gmail" 当成应用名。
        let app = snapshot(None, "Inbox - Gmail", CHROME);
        assert_eq!(resolve_name(&app), "Google Chrome");

        let with_description = snapshot(Some("Google Chrome"), "Inbox - Gmail", CHROME);
        assert_eq!(resolve_name(&with_description), "Google Chrome");
    }

    #[test]
    fn browser_mapping_covers_edge_and_firefox() {
        assert_eq!(browser_app_name(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"), Some("Microsoft Edge"));
        assert_eq!(browser_app_name(r"C:\Program Files\Mozilla Firefox\firefox.exe"), Some("Mozilla Firefox"));
        assert_eq!(browser_app_name(VSCODE), None);
    }

    #[test]
    fn capture_app_keeps_contract_shape_without_a_foreground_window() {
        // 无桌面 / 无前台窗口时也必须返回可序列化的契约，不能让前端拿到 undefined。
        let app = capture_app();
        assert!(!app.name.is_empty());
        assert!(!app.executable.contains('\\'));
    }
}
