use tauri::{AppHandle, Manager, PhysicalSize};

use crate::window::monitors;
use crate::window::BarsState;

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
    fn bar_height_stays_within_the_design_range() {
        assert_eq!(clamp_bar_height(10), 28);
        assert_eq!(clamp_bar_height(30), 30);
        assert_eq!(clamp_bar_height(100), 36);
    }
}
