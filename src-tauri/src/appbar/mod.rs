use std::ffi::c_void;
use windows_sys::Win32::Foundation::{HWND, RECT};
use windows_sys::Win32::UI::Shell::{
    SHAppBarMessage, ABE_TOP, ABM_NEW, ABM_QUERYPOS, ABM_REMOVE, ABM_SETPOS, APPBARDATA,
};

/// 顶部条带：从主屏左上角起算，宽度铺满。
pub fn bar_rect(top: i32, width: i32, height: i32) -> RECT {
    RECT {
        left: 0,
        top,
        right: width,
        bottom: top + height,
    }
}

/// ABM_QUERYPOS 会按当前工作区裁剪边缘，必须把底边改回 top + height，
/// 否则 shell 可能把条带压扁，最大化窗口仍会压住顶栏。
pub fn pin_height(rect: RECT, height: i32) -> RECT {
    RECT {
        left: rect.left,
        top: rect.top,
        right: rect.right,
        bottom: rect.top + height,
    }
}

pub fn register(hwnd: HWND, rect: RECT) -> bool {
    if hwnd.is_null() {
        return false;
    }
    unsafe {
        let mut data = app_bar_data(hwnd);
        if SHAppBarMessage(ABM_NEW, &mut data) == 0 {
            return false;
        }
        data.uEdge = ABE_TOP;
        data.rc = rect;
        SHAppBarMessage(ABM_QUERYPOS, &mut data);
        data.rc = pin_height(data.rc, rect.bottom - rect.top);
        SHAppBarMessage(ABM_SETPOS, &mut data) != 0
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
    fn pin_height_restores_the_bar_after_querypos_shrinks_it() {
        // QUERYPOS 会按工作区裁剪，若直接把裁剪结果交给 SETPOS，最大化窗口会盖住顶栏。
        let queried = RECT {
            left: 0,
            top: 0,
            right: 1920,
            bottom: 12,
        };
        let pinned = pin_height(queried, 30);
        assert_eq!(pinned.bottom - pinned.top, 30);
        assert_eq!(pinned.right, 1920);
    }

    #[test]
    fn null_handles_are_ignored_for_register_and_unregister() {
        assert!(!register(std::ptr::null_mut(), bar_rect(0, 1920, 30)));
        unregister(std::ptr::null_mut());
    }
}
