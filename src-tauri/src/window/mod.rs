pub mod popup_pool;

use tauri::window::{Effect, EffectsBuilder};
use tauri::{App, Manager, PhysicalPosition, PhysicalSize, WebviewWindow};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    GetSystemMetrics, GetWindowLongPtrW, SetWindowLongPtrW, GWL_EXSTYLE, SM_CXSCREEN,
    WS_EX_NOACTIVATE,
};

/// 与 src/styles/index.css 的 --lithe-bar-height 保持一致，一致性由测试钉住（坑 #25）。
pub const BAR_HEIGHT: u32 = 30;

pub fn configure(app: &App) -> Result<(), String> {
    let window = app
        .get_webview_window("main")
        .ok_or_else(|| "主窗口 main 未创建".to_string())?;

    // 配置里的 width 只是启动占位值，运行时按主屏实际宽度铺满；M4 再逐屏处理混合 DPI。
    let (screen_width, bar_height) = geometry(app, &window);
    if screen_width > 0 {
        window
            .set_size(PhysicalSize::new(screen_width, bar_height))
            .map_err(|error| error.to_string())?;
    }
    window
        .set_position(PhysicalPosition::new(0, 0))
        .map_err(|error| error.to_string())?;

    // NOACTIVATE 保证点击顶栏不会抢走前台窗口，否则 M2 的窗口控制会操作错对象。
    set_no_activate(&window);

    // 配置 visible=false，几何与样式就位后再显示，避免启动瞬间闪出 800px 的窄窗口。
    window.show().map_err(|error| error.to_string())?;

    // 先注册 AppBar 再钉住位置：ABM_SETPOS 会把窗口移到条带矩形，
    // 若工作区有残留，条带会被下移，窗口就会盖住最大化窗口的标题栏。
    if screen_width > 0 {
        crate::appbar::register_for_window(&window, screen_width as i32, bar_height as i32);
        window
            .set_position(PhysicalPosition::new(0, 0))
            .map_err(|error| error.to_string())?;
    }

    pin_position(&window);
    apply_acrylic(&window);
    Ok(())
}

/// 注册 AppBar 后 shell 会异步把窗口挪进工作区（实测 y 由 0 变成 30），
/// 此时窗口正好盖住最大化窗口的标题栏。窗口事件里把位置钉死在 (0,0)；
/// M4 多显示器时这里要改成按当前显示器原点钉住。
fn pin_position(window: &WebviewWindow) {
    let pinned = window.clone();
    window.on_window_event(move |event| {
        if let tauri::WindowEvent::Moved(position) = event {
            if position.x != 0 || position.y != 0 {
                let _ = pinned.set_position(PhysicalPosition::new(0, 0));
            }
        }
    });
}

/// CSS 的 30px 是逻辑像素，物理高度必须乘 DPI 缩放，否则 125% 缩放下内容会被裁切。
pub fn physical_bar_height(scale: f64) -> u32 {
    (BAR_HEIGHT as f64 * scale).round() as u32
}

fn geometry(app: &App, window: &WebviewWindow) -> (u32, u32) {
    let monitor = window
        .current_monitor()
        .ok()
        .flatten()
        .or_else(|| app.primary_monitor().ok().flatten());
    let scale = monitor.as_ref().map(|m| m.scale_factor()).unwrap_or(1.0);
    let width = monitor
        .as_ref()
        .map(|m| m.size().width)
        .filter(|width| *width > 0)
        .unwrap_or_else(|| unsafe { GetSystemMetrics(SM_CXSCREEN) as u32 });
    (width, physical_bar_height(scale))
}

fn set_no_activate(window: &WebviewWindow) {
    if let Ok(hwnd) = window.hwnd() {
        // tauri 用的是 windows crate 的 HWND newtype，转成 windows-sys 的裸指针。
        let hwnd_sys = (hwnd.0 as isize) as *mut std::ffi::c_void;
        unsafe {
            let style = GetWindowLongPtrW(hwnd_sys, GWL_EXSTYLE);
            SetWindowLongPtrW(
                hwnd_sys,
                GWL_EXSTYLE,
                style | WS_EX_NOACTIVATE as isize,
            );
        }
    }
}

fn apply_acrylic(window: &WebviewWindow) {
    // 失败只影响观感，不阻断启动；前端还有一层半透明着色兜底。
    let _ = window.set_effects(EffectsBuilder::new().effect(Effect::Acrylic).build());
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn main_window_config_stays_borderless_topmost_and_unfocused() {
        // 这些字段被改回默认值时只会表现为「抢焦点 / 进任务栏」，肉眼才发现，故钉成断言。
        let config = include_str!("../../tauri.conf.json");
        for field in [
            "\"transparent\": true",
            "\"decorations\": false",
            "\"alwaysOnTop\": true",
            "\"skipTaskbar\": true",
            "\"focus\": false",
            "\"resizable\": false",
            "\"visible\": false",
        ] {
            assert!(config.contains(field), "主窗口配置缺少 {field}");
        }
    }

    #[test]
    fn bar_height_matches_the_css_token() {
        // CSS 与 Rust 各写一份高度，漂移会让顶栏被裁切或留下空隙（坑 #25）。
        let css = include_str!("../../../src/styles/index.css");
        let expected = format!("--lithe-bar-height: {BAR_HEIGHT}px");
        assert!(css.contains(&expected), "CSS 与 Rust 的状态栏高度不一致");
    }

    #[test]
    fn physical_bar_height_scales_with_dpi() {
        // 96 DPI 下 30 逻辑像素 = 30 物理；120 DPI 下必须变成 38，否则内容被裁。
        assert_eq!(physical_bar_height(1.0), 30);
        assert_eq!(physical_bar_height(1.25), 38);
        assert_eq!(physical_bar_height(1.5), 45);
    }
}
