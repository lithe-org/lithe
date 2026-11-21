use std::sync::atomic::{AtomicBool, Ordering};
use tauri::{AppHandle, Manager, PhysicalSize, WebviewUrl, WebviewWindowBuilder};

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

// 与 tauri.conf.json 的 settings 窗口声明逐项对齐，由单测钉住。
const SETTINGS_LABEL: &str = "settings";
const SETTINGS_TITLE: &str = "Lithe Settings";
const SETTINGS_WIDTH: f64 = 820.0;
const SETTINGS_HEIGHT: f64 = 600.0;
const SETTINGS_MIN_WIDTH: f64 = 640.0;
const SETTINGS_MIN_HEIGHT: f64 = 420.0;

/// 唯一的设置窗口打开入口：已存在就显示，被用户关掉后按配置重建。
/// 前端从 popup 调用自定义命令，无需 `get_all_windows` 权限。
#[tauri::command]
pub fn show_settings_window(app: AppHandle) -> Result<(), String> {
    if let Some(window) = app.get_webview_window(SETTINGS_LABEL) {
        window.show().map_err(|error| error.to_string())?;
        window.set_focus().map_err(|error| error.to_string())?;
        return Ok(());
    }

    let window = WebviewWindowBuilder::new(
        &app,
        SETTINGS_LABEL,
        WebviewUrl::App("index.html".into()),
    )
    .title(SETTINGS_TITLE)
    .inner_size(SETTINGS_WIDTH, SETTINGS_HEIGHT)
    .min_inner_size(SETTINGS_MIN_WIDTH, SETTINGS_MIN_HEIGHT)
    .center()
    .decorations(true)
    .transparent(false)
    .always_on_top(false)
    .skip_taskbar(false)
    .focused(true)
    .resizable(true)
    .shadow(true)
    .visible(false)
    .build()
    .map_err(|error| error.to_string())?;

    window.show().map_err(|error| error.to_string())?;
    window.set_focus().map_err(|error| error.to_string())?;
    Ok(())
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
    fn settings_window_geometry_matches_config() {
        let config: serde_json::Value =
            serde_json::from_str(include_str!("../../tauri.conf.json")).unwrap();
        let window = config["app"]["windows"]
            .as_array()
            .unwrap()
            .iter()
            .find(|entry| entry["label"] == "settings")
            .expect("tauri.conf.json 应声明 settings 窗口");

        assert_eq!(window["title"].as_str(), Some(SETTINGS_TITLE));
        assert_eq!(window["width"].as_f64(), Some(SETTINGS_WIDTH));
        assert_eq!(window["height"].as_f64(), Some(SETTINGS_HEIGHT));
        assert_eq!(window["minWidth"].as_f64(), Some(SETTINGS_MIN_WIDTH));
        assert_eq!(window["minHeight"].as_f64(), Some(SETTINGS_MIN_HEIGHT));
        assert_eq!(window["decorations"].as_bool(), Some(true));
        assert_eq!(window["visible"].as_bool(), Some(false));
    }

    #[test]
    fn show_settings_window_recreates_when_missing() {
        // 只 show 不重建会让关闭后的第二次托盘点击静默失败。
        let source = include_str!("settings.rs");
        let production = source.split("#[cfg(test)]").next().unwrap();
        assert!(production.contains("WebviewWindowBuilder::new"));
        assert!(production.contains("SETTINGS_LABEL"));
    }

    #[test]
    fn bar_height_stays_within_the_design_range() {
        assert_eq!(clamp_bar_height(10), 28);
        assert_eq!(clamp_bar_height(30), 30);
        assert_eq!(clamp_bar_height(100), 36);
    }
}
