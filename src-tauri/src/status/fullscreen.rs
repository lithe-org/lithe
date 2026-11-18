use windows_sys::Win32::Foundation::{HWND, RECT};
use windows_sys::Win32::UI::Shell::{
    SHQueryUserNotificationState, QUNS_PRESENTATION_MODE, QUNS_RUNNING_D3D_FULL_SCREEN,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{GetForegroundWindow, GetWindowRect};

use crate::window::monitors::{self, Bounds};

/// DWM 隐藏边框与 AppBar 让位会造成几像素偏差，比对时留一点余量。
pub const FULLSCREEN_TOLERANCE: i32 = 2;

/// 前台窗口是否铺满整块显示器。
pub fn covers_monitor(window: Bounds, monitor: Bounds, tolerance: i32) -> bool {
    window.left <= monitor.left + tolerance
        && window.top <= monitor.top + tolerance
        && window.right >= monitor.right - tolerance
        && window.bottom >= monitor.bottom - tolerance
}

fn is_d3d_fullscreen() -> bool {
    let mut state = 0i32;
    // SHQueryUserNotificationState 成功返回 S_OK(0)。
    if unsafe { SHQueryUserNotificationState(&mut state) } != 0 {
        return false;
    }
    matches!(
        state,
        QUNS_RUNNING_D3D_FULL_SCREEN | QUNS_PRESENTATION_MODE
    )
}

/// PRD 5.7：D3D 独占全屏直接判定；无边框全屏窗口靠矩形兜底。
/// 普通最大化窗口因为 AppBar 让位只覆盖工作区，不会误判。
pub fn is_fullscreen_active() -> bool {
    if is_d3d_fullscreen() {
        return true;
    }

    let hwnd: HWND = unsafe { GetForegroundWindow() };
    if hwnd.is_null() {
        return false;
    }
    let mut raw: RECT = unsafe { std::mem::zeroed() };
    if unsafe { GetWindowRect(hwnd, &mut raw) } == 0 {
        return false;
    }

    let window = Bounds::from_rect(raw);
    let Ok(displays) = monitors::enumerate() else {
        return false;
    };
    displays
        .iter()
        .any(|display| covers_monitor(window, display.bounds, FULLSCREEN_TOLERANCE))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn bounds(left: i32, top: i32, right: i32, bottom: i32) -> Bounds {
        Bounds {
            left,
            top,
            right,
            bottom,
        }
    }

    #[test]
    fn a_window_covering_the_monitor_counts_as_fullscreen() {
        let monitor = bounds(0, 0, 1920, 1080);
        assert!(covers_monitor(monitor, monitor, FULLSCREEN_TOLERANCE));
    }

    #[test]
    fn a_maximized_window_under_the_bar_is_not_fullscreen() {
        // 最大化只覆盖工作区（top=30），不能因为「铺满宽度」就误判。
        let monitor = bounds(0, 0, 1920, 1080);
        let maximized = bounds(0, 30, 1920, 1080);
        assert!(!covers_monitor(maximized, monitor, FULLSCREEN_TOLERANCE));
    }

    #[test]
    fn tolerance_absorbs_hidden_border_but_not_a_real_gap() {
        let monitor = bounds(0, 0, 1920, 1080);
        assert!(covers_monitor(
            bounds(-1, -1, 1921, 1081),
            monitor,
            FULLSCREEN_TOLERANCE
        ));
        // 内缩超过容差说明四周露出桌面，不是全屏。
        assert!(!covers_monitor(
            bounds(3, 3, 1917, 1077),
            monitor,
            FULLSCREEN_TOLERANCE
        ));
    }

    #[test]
    fn secondary_monitor_negative_origin_is_supported() {
        let monitor = bounds(-1920, 0, 0, 1080);
        assert!(covers_monitor(monitor, monitor, FULLSCREEN_TOLERANCE));
        assert!(!covers_monitor(
            bounds(0, 0, 1920, 1080),
            monitor,
            FULLSCREEN_TOLERANCE
        ));
    }
}
