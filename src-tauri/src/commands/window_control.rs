use std::sync::Mutex;
use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    GetForegroundWindow, GetWindowLongPtrW, IsZoomed, SetWindowPos, ShowWindow, GWL_EXSTYLE,
    HWND_NOTOPMOST, HWND_TOPMOST, SW_MAXIMIZE, SW_MINIMIZE, SW_RESTORE, SWP_NOACTIVATE,
    SWP_NOMOVE, SWP_NOSIZE, WS_EX_TOPMOST,
};

use crate::commands::system::ForegroundApp;
use crate::status::foreground::{app_for_window, is_controllable_window};

// 鼠标按下瞬间捕获的目标：点击时前台可能已经变了，必须在按下时就记下来。
static CONTROL_TARGET: Mutex<Option<isize>> = Mutex::new(None);

#[tauri::command]
pub fn capture_control_target() -> Option<ForegroundApp> {
    let hwnd: HWND = unsafe { GetForegroundWindow() };
    if !is_controllable_window(hwnd) {
        return None;
    }
    remember(hwnd);
    Some(app_for_window(hwnd))
}

#[tauri::command]
pub fn toggle_topmost() -> Result<(), String> {
    with_target(|hwnd| unsafe {
        let insert_after = if is_topmost_style(GetWindowLongPtrW(hwnd, GWL_EXSTYLE)) {
            HWND_NOTOPMOST
        } else {
            HWND_TOPMOST
        };
        let changed = SetWindowPos(
            hwnd,
            insert_after,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE,
        );
        if changed == 0 {
            return Err("切换置顶状态失败".to_string());
        }
        Ok(())
    })
}

#[tauri::command]
pub fn toggle_maximize() -> Result<(), String> {
    with_target(|hwnd| unsafe {
        ShowWindow(hwnd, maximize_verb(IsZoomed(hwnd) != 0));
        Ok(())
    })
}

/// 已最大化时点击应还原，否则最大化——反过来会让按钮永远只能放大。
pub fn maximize_verb(zoomed: bool) -> i32 {
    if zoomed {
        SW_RESTORE
    } else {
        SW_MAXIMIZE
    }
}

#[tauri::command]
pub fn minimize_foreground() -> Result<(), String> {
    with_target(|hwnd| unsafe {
        ShowWindow(hwnd, SW_MINIMIZE);
        Ok(())
    })
}

pub fn is_topmost_style(ex_style: isize) -> bool {
    ex_style & WS_EX_TOPMOST as isize != 0
}

fn with_target(action: impl FnOnce(HWND) -> Result<(), String>) -> Result<(), String> {
    let hwnd = current_target().ok_or("当前没有可控制的窗口")?;
    action(hwnd)
}

fn current_target() -> Option<HWND> {
    let stored = CONTROL_TARGET.lock().ok().and_then(|target| *target);
    let hwnd = stored
        .map(|raw| raw as HWND)
        .unwrap_or_else(|| unsafe { GetForegroundWindow() });
    if is_controllable_window(hwnd) {
        Some(hwnd)
    } else {
        None
    }
}

fn remember(hwnd: HWND) {
    if let Ok(mut target) = CONTROL_TARGET.lock() {
        *target = Some(hwnd as isize);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn topmost_style_is_detected_from_the_ex_style_bits() {
        // 置顶状态只能从 WS_EX_TOPMOST 位判断，不能凭调用历史猜测。
        assert!(is_topmost_style(WS_EX_TOPMOST as isize));
        assert!(is_topmost_style(WS_EX_TOPMOST as isize | 0x0000_0100));
        assert!(!is_topmost_style(0));
    }

    #[test]
    fn maximize_verb_restores_when_already_zoomed() {
        assert_eq!(maximize_verb(true), SW_RESTORE);
        assert_eq!(maximize_verb(false), SW_MAXIMIZE);
    }
}
