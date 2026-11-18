use std::sync::mpsc::{self, Sender};
use std::sync::Mutex;
use tauri::AppHandle;
use windows_sys::Win32::Foundation::{HINSTANCE, HWND, LPARAM, LRESULT, RECT, WPARAM};
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DispatchMessageW, GetMessageW, HWND_MESSAGE, RegisterClassW,
    TranslateMessage, MSG, WNDCLASSW, WM_DISPLAYCHANGE,
};
use windows_sys::Win32::Graphics::Gdi::{
    EnumDisplayMonitors, GetMonitorInfoW, HDC, HMONITOR, MONITORINFO,
};
use windows_sys::Win32::UI::HiDpi::{GetDpiForMonitor, MDT_EFFECTIVE_DPI};
use windows_sys::Win32::UI::WindowsAndMessaging::MONITORINFOF_PRIMARY;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Bounds {
    pub left: i32,
    pub top: i32,
    pub right: i32,
    pub bottom: i32,
}

impl Bounds {
    pub fn width(&self) -> u32 {
        (self.right - self.left).max(0) as u32
    }

    pub fn height(&self) -> u32 {
        (self.bottom - self.top).max(0) as u32
    }

    fn from_rect(rect: RECT) -> Self {
        Self {
            left: rect.left,
            top: rect.top,
            right: rect.right,
            bottom: rect.bottom,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Display {
    /// HMONITOR 裸值，进程内用于按屏取 DPI。
    pub handle: isize,
    pub bounds: Bounds,
    pub work_area: Bounds,
    pub primary: bool,
}

/// 枚举顺序不保证主屏在前；主屏排第一，main 窗口才始终落在主屏。
pub fn sort_primary_first(mut displays: Vec<Display>) -> Vec<Display> {
    displays.sort_by_key(|display| !display.primary);
    displays
}

pub fn enumerate() -> Result<Vec<Display>, String> {
    let mut displays: Vec<Display> = Vec::new();
    unsafe {
        let ok = EnumDisplayMonitors(
            std::ptr::null_mut(),
            std::ptr::null(),
            Some(enum_callback),
            &mut displays as *mut Vec<Display> as LPARAM,
        );
        if ok == 0 {
            return Err("枚举显示器失败".to_string());
        }
    }
    Ok(sort_primary_first(displays))
}

unsafe extern "system" fn enum_callback(
    monitor: HMONITOR,
    _hdc: HDC,
    _clip: *mut RECT,
    data: LPARAM,
) -> i32 {
    let displays = unsafe { &mut *(data as *mut Vec<Display>) };
    let mut info: MONITORINFO = unsafe { std::mem::zeroed() };
    info.cbSize = std::mem::size_of::<MONITORINFO>() as u32;
    if unsafe { GetMonitorInfoW(monitor, &mut info) } == 0 {
        return 1;
    }
    // panic 不能穿过 FFI 边界，分配失败就跳过这块屏。
    let result = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        displays.push(Display {
            handle: monitor as isize,
            bounds: Bounds::from_rect(info.rcMonitor),
            work_area: Bounds::from_rect(info.rcWork),
            primary: info.dwFlags & MONITORINFOF_PRIMARY != 0,
        });
    }));
    if result.is_err() {
        0
    } else {
        1
    }
}

pub fn scale_from_dpi(dpi: u32) -> f64 {
    if dpi == 0 {
        return 1.0;
    }
    dpi as f64 / 96.0
}

/// 每块屏各自取有效 DPI，混合 DPI 下不能沿用主屏的缩放值。
pub fn dpi_for(handle: isize) -> u32 {
    let mut dpi_x = 0u32;
    let mut dpi_y = 0u32;
    let hr = unsafe {
        GetDpiForMonitor(
            handle as HMONITOR,
            MDT_EFFECTIVE_DPI,
            &mut dpi_x,
            &mut dpi_y,
        )
    };
    if hr == 0 && dpi_x > 0 {
        dpi_x
    } else {
        96
    }
}

pub fn scale_factor(handle: isize) -> f64 {
    scale_from_dpi(dpi_for(handle))
}

/// 隐藏的消息窗口把 WM_DISPLAYCHANGE 转成 channel 信号。
static CHANGE_SENDER: Mutex<Option<Sender<()>>> = Mutex::new(None);

pub fn watch(app: AppHandle) {
    let (sender, receiver) = mpsc::channel();
    if let Ok(mut current) = CHANGE_SENDER.lock() {
        *current = Some(sender);
    }

    std::thread::spawn(|| unsafe { run_watcher_loop() });
    std::thread::spawn(move || {
        while receiver.recv().is_ok() {
            let handle = app.clone();
            // 窗口的创建与销毁必须在主线程做。
            let _ = app.run_on_main_thread(move || {
                let _ = crate::window::resync_displays(&handle);
            });
        }
    });
}

unsafe extern "system" fn watcher_proc(
    hwnd: HWND,
    message: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    if message == WM_DISPLAYCHANGE {
        if let Ok(sender) = CHANGE_SENDER.lock() {
            if let Some(tx) = sender.as_ref() {
                let _ = tx.send(());
            }
        }
    }
    unsafe { DefWindowProcW(hwnd, message, wparam, lparam) }
}

unsafe fn run_watcher_loop() {
    let class_name: Vec<u16> = "LitheDisplayWatcher\0".encode_utf16().collect();
    let instance: HINSTANCE = unsafe { GetModuleHandleW(std::ptr::null()) };
    let mut class: WNDCLASSW = unsafe { std::mem::zeroed() };
    class.lpfnWndProc = Some(watcher_proc);
    class.hInstance = instance;
    class.lpszClassName = class_name.as_ptr();
    if unsafe { RegisterClassW(&class) } == 0 {
        return;
    }

    // message-only 窗口不出现在屏幕上，只接收 WM_DISPLAYCHANGE 广播。
    let hwnd = unsafe {
        CreateWindowExW(
            0,
            class_name.as_ptr(),
            std::ptr::null(),
            0,
            0,
            0,
            0,
            0,
            HWND_MESSAGE,
            std::ptr::null_mut(),
            instance,
            std::ptr::null(),
        )
    };
    if hwnd.is_null() {
        return;
    }

    let mut message: MSG = unsafe { std::mem::zeroed() };
    while unsafe { GetMessageW(&mut message, std::ptr::null_mut(), 0, 0) } > 0 {
        unsafe {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn display(handle: isize, left: i32, primary: bool) -> Display {
        Display {
            handle,
            bounds: Bounds {
                left,
                top: 0,
                right: left + 1920,
                bottom: 1080,
            },
            work_area: Bounds {
                left,
                top: 30,
                right: left + 1920,
                bottom: 1050,
            },
            primary,
        }
    }

    #[test]
    fn bounds_dimensions_are_non_negative() {
        let bounds = Bounds {
            left: -1920,
            top: 0,
            right: 0,
            bottom: 1080,
        };
        assert_eq!((bounds.width(), bounds.height()), (1920, 1080));
    }

    #[test]
    fn primary_monitor_comes_first_regardless_of_enumeration_order() {
        let sorted = sort_primary_first(vec![
            display(2, 1920, false),
            display(1, 0, true),
        ]);
        assert_eq!(sorted[0].handle, 1);
        assert_eq!(sorted[1].handle, 2);
    }

    #[test]
    fn effective_dpi_maps_to_its_own_scale_factor() {
        // 主屏 96 DPI = 1.0，副屏 144 DPI = 1.5，不能混用。
        assert_eq!(scale_from_dpi(96), 1.0);
        assert_eq!(scale_from_dpi(144), 1.5);
        assert_eq!(scale_from_dpi(0), 1.0);
    }
}
