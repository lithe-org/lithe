use std::time::Duration;
use serde::Serialize;
use tauri::{AppHandle, Emitter};
use windows_sys::Win32::Foundation::{HWND, RECT};
use windows_sys::Win32::UI::Shell::{
    SHQueryUserNotificationState, QUNS_PRESENTATION_MODE, QUNS_RUNNING_D3D_FULL_SCREEN,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{GetForegroundWindow, GetWindowRect};

use crate::commands::EVENT_FULLSCREEN_CHANGED;
use crate::window::monitors::{self, Bounds};

/// DWM 隐藏边框与 AppBar 让位会造成几像素偏差，比对时留一点余量。
pub const FULLSCREEN_TOLERANCE: i32 = 2;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FullscreenState {
    pub active: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FullscreenTransition {
    Hide(isize),
    Show(isize),
}

/// 从一个全屏目标切到另一个：先恢复旧屏，再隐藏新屏。
pub fn transitions(previous: Option<isize>, current: Option<isize>) -> Vec<FullscreenTransition> {
    let mut result = Vec::new();
    if let Some(old) = previous {
        if Some(old) != current {
            result.push(FullscreenTransition::Show(old));
        }
    }
    if let Some(new) = current {
        if Some(new) != previous {
            result.push(FullscreenTransition::Hide(new));
        }
    }
    result
}

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

fn foreground_rect() -> Option<Bounds> {
    let hwnd: HWND = unsafe { GetForegroundWindow() };
    if hwnd.is_null() {
        return None;
    }
    let mut raw: RECT = unsafe { std::mem::zeroed() };
    if unsafe { GetWindowRect(hwnd, &mut raw) } == 0 {
        return None;
    }
    Some(Bounds::from_rect(raw))
}

/// 返回被全屏窗口覆盖的显示器句柄，供隐藏/恢复对应顶栏。
pub fn fullscreen_monitor() -> Option<isize> {
    let window = foreground_rect()?;
    let displays = monitors::enumerate().ok()?;
    if let Some(display) = displays
        .iter()
        .find(|display| covers_monitor(window, display.bounds, FULLSCREEN_TOLERANCE))
    {
        return Some(display.handle);
    }
    // D3D 独占全屏偶尔拿不到精确矩形，用窗口中心所在屏兜底。
    if is_d3d_fullscreen() {
        let center_x = window.left + (window.right - window.left) / 2;
        let center_y = window.top + (window.bottom - window.top) / 2;
        return displays
            .iter()
            .find(|display| {
                center_x >= display.bounds.left
                    && center_x <= display.bounds.right
                    && center_y >= display.bounds.top
                    && center_y <= display.bounds.bottom
            })
            .map(|display| display.handle);
    }
    None
}

pub fn is_fullscreen_active() -> bool {
    fullscreen_monitor().is_some() || is_d3d_fullscreen()
}

/// 轮询全屏状态；窗口操作必须投递到主线程执行。
pub fn start(app: AppHandle) {
    std::thread::spawn(move || {
        let mut previous: Option<isize> = None;
        loop {
            let current = fullscreen_monitor();
            if current != previous {
                let changes = if crate::commands::settings::auto_hide_enabled() {
                    transitions(previous, current)
                } else {
                    // 用户在全屏中关掉自动隐藏时，先把已隐藏的顶栏恢复回来。
                    previous
                        .map(FullscreenTransition::Show)
                        .into_iter()
                        .collect()
                };
                let state = FullscreenState {
                    active: current.is_some(),
                };
                let handle = app.clone();
                let _ = app.run_on_main_thread(move || {
                    for change in changes {
                        match change {
                            FullscreenTransition::Show(monitor) => {
                                let _ =
                                    crate::window::show_bar_for_monitor(&handle, monitor);
                            }
                            FullscreenTransition::Hide(monitor) => {
                                let _ =
                                    crate::window::hide_bar_for_monitor(&handle, monitor);
                            }
                        }
                    }
                    let _ = handle.emit(EVENT_FULLSCREEN_CHANGED, state);
                });
                previous = current;
            }
            std::thread::sleep(Duration::from_millis(500));
        }
    });
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
    fn entering_fullscreen_hides_only_the_covered_monitor() {
        assert_eq!(
            transitions(None, Some(2)),
            vec![FullscreenTransition::Hide(2)]
        );
        assert!(transitions(None, None).is_empty());
        assert!(transitions(Some(2), Some(2)).is_empty());
    }

    #[test]
    fn exiting_fullscreen_shows_the_previous_monitor() {
        assert_eq!(
            transitions(Some(2), None),
            vec![FullscreenTransition::Show(2)]
        );
    }

    #[test]
    fn switching_fullscreen_between_monitors_restores_then_hides() {
        assert_eq!(
            transitions(Some(1), Some(2)),
            vec![
                FullscreenTransition::Show(1),
                FullscreenTransition::Hide(2)
            ]
        );
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
