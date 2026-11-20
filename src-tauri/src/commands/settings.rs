use std::sync::atomic::{AtomicBool, Ordering};
use tauri::{AppHandle, Manager, PhysicalSize};

use crate::window::monitors;
use crate::window::BarsState;

/// 全屏自动隐藏由设置页控制；默认开启，关闭后轮询只恢复不再隐藏。
static AUTO_HIDE_FULLSCREEN: AtomicBool = AtomicBool::new(true);

pub fn auto_hide_enabled() -> bool {
    AUTO_HIDE_FULLSCREEN.load(Ordering::Relaxed)
}

#[tauri::command]
pub fn set_fullscreen_autohide(enabled: bool) {
    AUTO_HIDE_FULLSCREEN.store(enabled, Ordering::Relaxed);
}

#[tauri::command]
pub fn get_fullscreen_autohide() -> bool {
    auto_hide_enabled()
}

/// 与前端 stores/settings.ts 的 28~36 保持一致。
pub fn clamp_bar_height(height: u32) -> u32 {
    height.clamp(28, 36)
}

#[tauri::command]
pub fn set_bar_height(app: AppHandle, height: u32) -> Result<(), String> {
    let logical = clamp_bar_height(height);
    let Some(state) = app.try_state::<BarsState>() else {
        return Ok(());
    };

    for bar in state.snapshot() {
        let Some(window) = app.get_webview_window(&bar.label) else {
            continue;
        };
        let Ok(size) = window.outer_size() else {
            continue;
        };
        let physical = (logical as f64 * monitors::scale_factor(bar.monitor_handle)).round() as u32;
        let _ = window.set_size(PhysicalSize::new(size.width, physical));

        // AppBar 预留高度变了，必须按新几何重注册，否则工作区留白对不上。
        crate::appbar::unregister_for_window(&window);
        if let Ok(position) = window.outer_position() {
            crate::appbar::register_for_window_at(
                &window,
                position.x,
                position.y,
                size.width as i32,
                physical as i32,
            );
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn fullscreen_autohide_toggles_back_and_forth() {
        let original = auto_hide_enabled();
        set_fullscreen_autohide(false);
        assert!(!get_fullscreen_autohide());
        set_fullscreen_autohide(true);
        assert!(get_fullscreen_autohide());
        set_fullscreen_autohide(original);
    }

    #[test]
    fn bar_height_stays_within_the_design_range() {
        assert_eq!(clamp_bar_height(10), 28);
        assert_eq!(clamp_bar_height(30), 30);
        assert_eq!(clamp_bar_height(100), 36);
    }
}
