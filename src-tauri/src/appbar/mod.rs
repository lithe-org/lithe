use std::ffi::c_void;
use std::sync::Mutex;
use windows_sys::Win32::Foundation::{HWND, RECT};
use windows_sys::Win32::UI::Shell::{
    SHAppBarMessage, ABE_TOP, ABM_NEW, ABM_QUERYPOS, ABM_REMOVE, ABM_SETPOS, APPBARDATA,
};

/// 每块屏一个 AppBar；退出阶段拿不到窗口，按记录的句柄逐个注销。
static REGISTERED: Mutex<Vec<isize>> = Mutex::new(Vec::new());

/// 条带矩形按显示器原点起算，副屏不再是 x=0。
pub fn bar_rect_at(left: i32, top: i32, width: i32, height: i32) -> RECT {
    RECT {
        left,
        top,
        right: left + width,
        bottom: top + height,
    }
}

pub fn bar_rect(top: i32, width: i32, height: i32) -> RECT {
    bar_rect_at(0, top, width, height)
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
    forget(hwnd);
}

/// 退出路径专用：不依赖窗口是否还在，清空记录后逐个注销。
pub fn unregister_registered() {
    let registered: Vec<isize> = REGISTERED
        .lock()
        .map(|mut registered| std::mem::take(&mut *registered))
        .unwrap_or_default();
    for raw in registered {
        unregister(raw as HWND);
    }
}

pub fn register_for_window_at(
    window: &tauri::WebviewWindow,
    left: i32,
    top: i32,
    width: i32,
    height: i32,
) -> bool {
    match window.hwnd() {
        Ok(hwnd) => register(
            (hwnd.0 as isize) as *mut c_void,
            bar_rect_at(left, top, width, height),
        ),
        Err(_) => false,
    }
}

pub fn register_for_window(window: &tauri::WebviewWindow, width: i32, height: i32) -> bool {
    register_for_window_at(window, 0, 0, width, height)
}

pub fn unregister_for_window(window: &tauri::WebviewWindow) {
    if let Ok(hwnd) = window.hwnd() {
        unregister((hwnd.0 as isize) as *mut c_void);
    }
}

fn remember(hwnd: HWND) {
    if let Ok(mut registered) = REGISTERED.lock() {
        let raw = hwnd as isize;
        if !registered.contains(&raw) {
            registered.push(raw);
        }
    }
}

fn forget(hwnd: HWND) {
    if let Ok(mut registered) = REGISTERED.lock() {
        let raw = hwnd as isize;
        registered.retain(|item| *item != raw);
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

    // 两个用例都改写全局注册表，必须串行执行。
    static TEST_LOCK: Mutex<()> = Mutex::new(());

    #[test]
    fn bar_rect_spans_the_full_width_at_the_top_edge() {
        let rect = bar_rect(0, 1920, 30);
        assert_eq!(
            (rect.left, rect.top, rect.right, rect.bottom),
            (0, 0, 1920, 30)
        );
    }

    #[test]
    fn bar_rect_at_uses_the_display_origin() {
        // 副屏在 (-1920, 0) 时条带不能从 0 起算，否则盖在主屏上。
        let rect = bar_rect_at(-1920, 0, 1920, 30);
        assert_eq!(
            (rect.left, rect.top, rect.right, rect.bottom),
            (-1920, 0, 0, 30)
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
    fn registered_handles_are_unique_and_removable() {
        let _guard = TEST_LOCK.lock().unwrap();
        let fake = 1234isize as HWND;
        REGISTERED.lock().unwrap().clear();
        remember(fake);
        remember(fake);
        assert_eq!(REGISTERED.lock().unwrap().len(), 1);
        forget(fake);
        assert!(REGISTERED.lock().unwrap().is_empty());
    }

    #[test]
    fn unregister_without_registration_is_a_no_op() {
        let _guard = TEST_LOCK.lock().unwrap();
        REGISTERED.lock().unwrap().clear();
        unregister_registered();
        assert!(REGISTERED.lock().unwrap().is_empty());
    }
}
