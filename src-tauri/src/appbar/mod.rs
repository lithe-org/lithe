use std::ffi::c_void;
use std::sync::Mutex;
use windows_sys::Win32::Foundation::{HWND, RECT};
use windows_sys::Win32::UI::Shell::{
    SHAppBarMessage, ABE_TOP, ABM_NEW, ABM_QUERYPOS, ABM_REMOVE, ABM_SETPOS, APPBARDATA,
};

// 退出阶段主窗口可能已销毁、拿不到 hwnd，因此单独记住注册成功的那一个句柄。
static REGISTERED_HWND: Mutex<Option<isize>> = Mutex::new(None);

/// 顶部条带：从主屏左上角起算，宽度铺满。
pub fn bar_rect(top: i32, width: i32, height: i32) -> RECT {
    RECT {
        left: 0,
        top,
        right: width,
        bottom: top + height,
    }
}

/// ABM_QUERYPOS 会按当前工作区裁剪边缘（工作区有残留时 top 会被下移），
/// 必须把上下边强制改回期望值，否则条带连同窗口一起被推进工作区，
/// 反而盖住最大化窗口的标题栏。
pub fn pin_bounds(rect: RECT, top: i32, height: i32) -> RECT {
    RECT {
        left: rect.left,
        top,
        right: rect.right,
        bottom: top + height,
    }
}

pub fn register(hwnd: HWND, rect: RECT) -> bool {
    if hwnd.is_null() {
        return false;
    }
    unsafe {
        // 热重载或重复调用时先移除旧注册，否则 shell 会叠加多条预留。
        let mut stale = app_bar_data(hwnd);
        SHAppBarMessage(ABM_REMOVE, &mut stale);

        let mut data = app_bar_data(hwnd);
        if SHAppBarMessage(ABM_NEW, &mut data) == 0 {
            return false;
        }
        data.uEdge = ABE_TOP;
        data.rc = rect;
        SHAppBarMessage(ABM_QUERYPOS, &mut data);
        data.rc = pin_bounds(data.rc, rect.top, rect.bottom - rect.top);
        let registered = SHAppBarMessage(ABM_SETPOS, &mut data) != 0;
        if registered {
            remember(hwnd);
        }
        registered
    }
}

pub fn unregister(hwnd: HWND) {
    if hwnd.is_null() {
        return;
    }
    unsafe {
        let mut data = app_bar_data(hwnd);
        SHAppBarMessage(ABM_REMOVE, &mut data);
    }
    forget();
}

/// 退出路径专用：不依赖主窗口是否还在，取出记录的句柄再注销。
/// 重复调用是安全的，取出后即清空。
pub fn unregister_registered() {
    if let Some(raw) = REGISTERED_HWND
        .lock()
        .ok()
        .and_then(|mut registered| registered.take())
    {
        unregister(raw as HWND);
    }
}

pub fn register_for_window(window: &tauri::WebviewWindow, width: i32, height: i32) -> bool {
    match window.hwnd() {
        Ok(hwnd) => register((hwnd.0 as isize) as *mut c_void, bar_rect(0, width, height)),
        Err(_) => false,
    }
}

pub fn unregister_for_window(window: &tauri::WebviewWindow) {
    if let Ok(hwnd) = window.hwnd() {
        unregister((hwnd.0 as isize) as *mut c_void);
    }
}

fn remember(hwnd: HWND) {
    if let Ok(mut registered) = REGISTERED_HWND.lock() {
        *registered = Some(hwnd as isize);
    }
}

fn forget() {
    if let Ok(mut registered) = REGISTERED_HWND.lock() {
        *registered = None;
    }
}

fn app_bar_data(hwnd: HWND) -> APPBARDATA {
    // APPBARDATA 是 POD，零化等价于按 MSDN 先 memset 再填 cbSize 的写法。
    let mut data: APPBARDATA = unsafe { std::mem::zeroed() };
    data.cbSize = std::mem::size_of::<APPBARDATA>() as u32;
    data.hWnd = hwnd;
    data
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn bar_rect_spans_the_full_width_at_the_top_edge() {
        let rect = bar_rect(0, 1920, 30);
        assert_eq!(
            (rect.left, rect.top, rect.right, rect.bottom),
            (0, 0, 1920, 30)
        );
    }

    #[test]
    fn pin_bounds_forces_the_bar_back_to_the_screen_top() {
        // 工作区有残留时 QUERYPOS 会返回 top=30，原样 SETPOS 会把窗口推进工作区。
        let queried = RECT {
            left: 0,
            top: 30,
            right: 1920,
            bottom: 42,
        };
        let pinned = pin_bounds(queried, 0, 30);
        assert_eq!((pinned.top, pinned.bottom), (0, 30));
        assert_eq!(pinned.right, 1920);
    }

    #[test]
    fn null_handles_are_ignored_for_register_and_unregister() {
        assert!(!register(std::ptr::null_mut(), bar_rect(0, 1920, 30)));
        unregister(std::ptr::null_mut());
    }

    #[test]
    fn exit_path_must_unregister_the_app_bar() {
        // 漏掉这一步只会表现为「退出后工作区顶部永久留白」，重启前难以发现。
        let lib_source = include_str!("../lib.rs");
        let exit_start = lib_source
            .find("RunEvent::Exit")
            .expect("lib.rs 缺少退出事件处理");
        assert!(lib_source[exit_start..].contains("unregister_registered"));
    }

    #[test]
    fn unregister_without_registration_is_a_no_op() {
        // 注册可能被系统策略阻止，退出清理必须容忍「从未注册成功」。
        unregister_registered();
        assert!(REGISTERED_HWND.lock().unwrap().is_none());
    }
}
