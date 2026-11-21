pub mod backdrop;
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

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BarChange {
    Remove { label: String },
    Add { label: String, monitor_handle: isize },
}

/// 按显示器句柄比较，而不是按 label：插入副屏时 label 会整体后移一位。
pub fn bar_diff(current: &[BarWindow], displays: &[Display]) -> Vec<BarChange> {
    let mut changes = Vec::new();
    for bar in current {
        if !displays
            .iter()
            .any(|display| display.handle == bar.monitor_handle)
        {
            changes.push(BarChange::Remove {
                label: bar.label.clone(),
            });
        }
    }
    for (index, display) in displays.iter().enumerate() {
        if !current
            .iter()
            .any(|bar| bar.monitor_handle == display.handle)
        {
            changes.push(BarChange::Add {
                label: bar_label(index),
                monitor_handle: display.handle,
            });
        }
    }
    changes
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

/// 显示器热插拔后重新对齐窗口：先移除已拔掉的屏，再给新屏建窗口。
pub fn resync_displays(app: &AppHandle) -> Result<(), String> {
    let displays = monitors::enumerate()?;
    if displays.is_empty() {
        return Err("没有可用的显示器".to_string());
    }
    let Some(state) = app.try_state::<BarsState>() else {
        // 启动阶段 configure_all 尚未 manage 状态，忽略提前到达的通知。
        return Ok(());
    };

    for change in bar_diff(&state.snapshot(), &displays) {
        match change {
            BarChange::Remove { label } => {
                if let Some(window) = app.get_webview_window(&label) {
                    crate::appbar::unregister_for_window(&window);
                    let _ = window.close();
                }
            }
            BarChange::Add {
                label,
                monitor_handle,
            } => {
                if let Some(display) = displays
                    .iter()
                    .find(|display| display.handle == monitor_handle)
                {
                    let window = create_bar_window(app, &label)
                        .map_err(|error| error.to_string())?;
                    configure_bar(&window, display)?;
                }
            }
        }
    }

    let bars = displays
        .iter()
        .enumerate()
        .map(|(index, display)| BarWindow {
            label: bar_label(index),
            monitor_handle: display.handle,
        })
        .collect();
    state.replace(bars);
    Ok(())
}

fn find_bar_window(app: &AppHandle, monitor_handle: isize) -> Result<WebviewWindow, String> {
    let state = app.state::<BarsState>();
    let bar = state
        .snapshot()
        .into_iter()
        .find(|bar| bar.monitor_handle == monitor_handle)
        .ok_or_else(|| "该显示器没有顶栏".to_string())?;
    app.get_webview_window(&bar.label)
        .ok_or_else(|| "顶栏窗口不存在".to_string())
}

/// 进入全屏：先注销 AppBar 归还工作区，再隐藏该屏的顶栏。
pub fn hide_bar_for_monitor(app: &AppHandle, monitor_handle: isize) -> Result<(), String> {
    let window = find_bar_window(app, monitor_handle)?;
    crate::appbar::unregister_for_window(&window);
    window.hide().map_err(|error| error.to_string())
}

/// 退出全屏：先显示顶栏，再按当前几何重新注册 AppBar 预留工作区。
pub fn show_bar_for_monitor(app: &AppHandle, monitor_handle: isize) -> Result<(), String> {
    let window = find_bar_window(app, monitor_handle)?;
    if window.is_visible().map_err(|error| error.to_string())? {
        // 重复通知时不能叠加 AppBar 注册，否则工作区会被多次压缩。
        return Ok(());
    }
    window.show().map_err(|error| error.to_string())?;

    if let (Ok(position), Ok(size)) = (window.outer_position(), window.outer_size()) {
        crate::appbar::register_for_window_at(
            &window,
            position.x,
            position.y,
            size.width as i32,
            size.height as i32,
        );
    }
    // shell 在 SETPOS 后会异步挪窗，pin_position 会把窗口钉回该屏原点。
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
    let height = bar_height_from_dpi(monitors::dpi_for(display.handle));
    let origin_x = display.bounds.left;
    let origin_y = display.bounds.top;

    window
        .set_size(PhysicalSize::new(width, height))
        .map_err(|error| error.to_string())?;
    window
        .set_position(PhysicalPosition::new(origin_x, origin_y))
        .map_err(|error| error.to_string())?;

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
    // DWM 应用毛玻璃会重建窗口样式，NOACTIVATE 必须最后再设一次，
    // 否则点击顶栏仍会把前台焦点抢过来（BUG 4）。
    set_no_activate(window);
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

/// 从某块屏的原始 DPI 直接得到物理条高，把「每屏独立计算」钉成可测纯函数。
pub fn bar_height_from_dpi(dpi: u32) -> u32 {
    physical_bar_height(monitors::scale_from_dpi(dpi))
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

pub(crate) fn apply_acrylic(window: &WebviewWindow) {
    if let Ok(hwnd) = window.hwnd() {
        let hwnd_sys = (hwnd.0 as isize) as *mut std::ffi::c_void;
        if backdrop::apply_acrylic_backdrop(hwnd_sys) {
            return;
        }
    }
    // 旧版 Windows 不支持 DWMWA_SYSTEMBACKDROP_TYPE 时回退到 Tauri 自带的 Effects。
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
    fn settings_window_is_opened_through_one_command_everywhere() {
        // 注册 + 单实例 + 托盘菜单 + 托盘左键必须共用同一命令，
        // 否则关闭后重建的兜底只覆盖部分入口。
        let lib = include_str!("../lib.rs");
        let count = lib.matches("show_settings_window").count();
        assert!(count >= 4, "设置窗口入口应统一，实际出现 {count} 次");
    }

    #[test]
    fn tray_icon_opens_settings_and_can_quit() {
        let lib = include_str!("../lib.rs");
        assert!(lib.contains("TrayIconBuilder::new()"));
        assert!(lib.contains("icons/32x32.png"));
        assert!(lib.contains("\"quit\" => app.exit(0)"));
    }

    #[test]
    fn no_activate_is_applied_after_acrylic() {
        // DWM 应用毛玻璃会重建扩展样式，早先设置会被冲掉（BUG 4）。
        let source = include_str!("mod.rs");
        // 只看生产代码，测试字符串本身也会命中函数名。
        let production = source.split("#[cfg(test)]").next().unwrap();
        let acrylic = production.find("apply_acrylic(window);").unwrap();
        let no_activate = production.rfind("set_no_activate(window);").unwrap();
        assert!(no_activate > acrylic);
    }

    #[test]
    fn settings_window_is_a_normal_decorated_window() {
        let config = include_str!("../../tauri.conf.json");
        assert!(config.contains("\"label\": \"settings\""));
        // 设置窗口要有标题栏，不能沿用顶栏的无边框配置。
        assert!(config.contains("\"decorations\": true"));
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

    fn display(handle: isize, left: i32) -> Display {
        Display {
            handle,
            bounds: monitors::Bounds {
                left,
                top: 0,
                right: left + 1920,
                bottom: 1080,
            },
            work_area: monitors::Bounds {
                left,
                top: 30,
                right: left + 1920,
                bottom: 1050,
            },
            primary: handle == 1,
        }
    }

    #[test]
    fn fullscreen_watcher_is_wired_to_both_directions() {
        // 只做隐藏不做恢复会让退出全屏后顶栏永久消失，必须双向都有调用。
        let watcher = include_str!("../status/fullscreen.rs");
        assert!(watcher.contains("show_bar_for_monitor"));
        assert!(watcher.contains("hide_bar_for_monitor"));
    }

    #[test]
    fn bar_diff_only_touches_added_and_removed_displays() {
        let current = vec![
            BarWindow {
                label: "main".to_string(),
                monitor_handle: 1,
            },
            BarWindow {
                label: "bar-1".to_string(),
                monitor_handle: 2,
            },
        ];
        let displays = vec![display(1, 0), display(3, 1920)];

        assert_eq!(
            bar_diff(&current, &displays),
            vec![
                BarChange::Remove {
                    label: "bar-1".to_string(),
                },
                BarChange::Add {
                    label: "bar-1".to_string(),
                    monitor_handle: 3,
                },
            ]
        );
    }

    #[test]
    fn bar_diff_ignores_reordering_of_the_same_displays() {
        let current = vec![
            BarWindow {
                label: "main".to_string(),
                monitor_handle: 1,
            },
            BarWindow {
                label: "bar-1".to_string(),
                monitor_handle: 2,
            },
        ];
        let displays = vec![display(1, 0), display(2, 1920)];

        assert!(bar_diff(&current, &displays).is_empty());
    }

    #[test]
    fn bar_height_comes_from_each_monitors_own_dpi() {
        // 主屏 96 DPI → 30px，副屏 144 DPI → 45px；不能共用主屏的 DPI。
        assert_eq!(bar_height_from_dpi(96), 30);
        assert_eq!(bar_height_from_dpi(120), 38);
        assert_eq!(bar_height_from_dpi(144), 45);
    }

    #[test]
    fn primary_screen_reuses_main_and_secondaries_get_numbered_labels() {
        assert_eq!(bar_label(0), "main");
        assert_eq!(bar_label(1), "bar-1");
        assert_eq!(bar_label(2), "bar-2");
    }
}
