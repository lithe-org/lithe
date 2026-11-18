pub mod monitors;
pub mod placement;
pub mod popup_pool;

use std::sync::Mutex;
use tauri::window::{Effect, EffectsBuilder};
use tauri::{
    AppHandle, Manager, PhysicalPosition, PhysicalSize, WebviewUrl, WebviewWindow,
    WebviewWindowBuilder, WindowEvent,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    GetWindowLongPtrW, SetWindowLongPtrW, GWL_EXSTYLE, WS_EX_NOACTIVATE,
};

use monitors::Display;

/// 与 src/styles/index.css 的 --lithe-bar-height 保持一致，一致性由测试钉住（坑 #25）。
pub const BAR_HEIGHT: u32 = 30;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BarWindow {
    pub label: String,
    pub monitor_handle: isize,
}

/// 当前每块屏对应的顶栏窗口；热插拔时据此增删。
#[derive(Debug, Default)]
pub struct BarsState {
    bars: Mutex<Vec<BarWindow>>,
}

impl BarsState {
    pub fn new(bars: Vec<BarWindow>) -> Self {
        Self {
            bars: Mutex::new(bars),
        }
    }

    pub fn snapshot(&self) -> Vec<BarWindow> {
        self.bars
            .lock()
            .map(|bars| bars.clone())
            .unwrap_or_default()
    }

    pub fn replace(&self, bars: Vec<BarWindow>) {
        if let Ok(mut current) = self.bars.lock() {
            *current = bars;
        }
    }
}

/// 主屏复用 tauri.conf.json 声明的 main，副屏按序命名 bar-N。
pub fn bar_label(index: usize) -> String {
    if index == 0 {
        "main".to_string()
    } else {
        format!("bar-{index}")
    }
}

pub fn configure_all(app: &AppHandle) -> Result<(), String> {
    let displays = monitors::enumerate()?;
    if displays.is_empty() {
        return Err("没有可用的显示器".to_string());
    }

    let mut bars = Vec::with_capacity(displays.len());
    for (index, display) in displays.iter().enumerate() {
        let label = bar_label(index);
        let window = match app.get_webview_window(&label) {
            Some(window) => window,
            None => create_bar_window(app, &label).map_err(|error| error.to_string())?,
        };
        configure_bar(&window, display)?;
        bars.push(BarWindow {
            label,
            monitor_handle: display.handle,
        });
    }

    app.manage(BarsState::new(bars));
    Ok(())
}

fn create_bar_window(app: &AppHandle, label: &str) -> tauri::Result<WebviewWindow> {
    // 配置与主窗口保持一致：透明、无边框、置顶、不抢焦点、不进任务栏。
    WebviewWindowBuilder::new(app, label, WebviewUrl::App("index.html".into()))
        .title("Lithe")
        .inner_size(800.0, BAR_HEIGHT as f64)
        .visible(false)
        .decorations(false)
        .transparent(true)
        .always_on_top(true)
        .skip_taskbar(true)
        .focused(false)
        .resizable(false)
        .shadow(false)
        .build()
}

fn configure_bar(window: &WebviewWindow, display: &Display) -> Result<(), String> {
    let width = display.bounds.width();
    let height = physical_bar_height(monitors::scale_factor(display.handle));
    let origin_x = display.bounds.left;
    let origin_y = display.bounds.top;

    window
        .set_size(PhysicalSize::new(width, height))
        .map_err(|error| error.to_string())?;
    window
        .set_position(PhysicalPosition::new(origin_x, origin_y))
        .map_err(|error| error.to_string())?;

    set_no_activate(window);

    // 配置 visible=false，几何与样式就位后再显示，避免闪出占位窗口。
    window.show().map_err(|error| error.to_string())?;

    // 先注册 AppBar 再钉住位置：SETPOS 会把窗口移到条带矩形。
    crate::appbar::register_for_window_at(
        window,
        origin_x,
        origin_y,
        width as i32,
        height as i32,
    );
    window
        .set_position(PhysicalPosition::new(origin_x, origin_y))
        .map_err(|error| error.to_string())?;

    pin_position(window, origin_x, origin_y);
    apply_acrylic(window);
    Ok(())
}

/// shell 在注册 AppBar 后会异步把窗口挪进工作区，这里按所属显示器原点钉住。
fn pin_position(window: &WebviewWindow, x: i32, y: i32) {
    let pinned = window.clone();
    window.on_window_event(move |event| {
        if let WindowEvent::Moved(position) = event {
            if position.x != x || position.y != y {
                let _ = pinned.set_position(PhysicalPosition::new(x, y));
            }
        }
    });
}

/// CSS 的 30px 是逻辑像素，物理高度必须乘该屏 DPI，否则高缩放下内容被裁切。
pub fn physical_bar_height(scale: f64) -> u32 {
    (BAR_HEIGHT as f64 * scale).round() as u32
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
    fn physical_bar_height_uses_each_monitors_own_dpi() {
        // 96 DPI 下 30 逻辑像素 = 30 物理；150% 副屏必须是 45。
        assert_eq!(physical_bar_height(1.0), 30);
        assert_eq!(physical_bar_height(1.25), 38);
        assert_eq!(physical_bar_height(1.5), 45);
    }

    #[test]
    fn primary_screen_reuses_main_and_secondaries_get_numbered_labels() {
        assert_eq!(bar_label(0), "main");
        assert_eq!(bar_label(1), "bar-1");
        assert_eq!(bar_label(2), "bar-2");
    }
}
