use tauri::{App, Manager, PhysicalPosition, PhysicalSize};
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
    let width = unsafe { GetSystemMetrics(SM_CXSCREEN) };
    if width > 0 {
        window
            .set_size(PhysicalSize::new(width as u32, BAR_HEIGHT))
            .map_err(|error| error.to_string())?;
    }
    window
        .set_position(PhysicalPosition::new(0, 0))
        .map_err(|error| error.to_string())?;

    // NOACTIVATE 保证点击顶栏不会抢走前台窗口，否则 M2 的窗口控制会操作错对象。
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

    // 配置 visible=false，几何与样式就位后再显示，避免启动瞬间闪出 800px 的窄窗口。
    window.show().map_err(|error| error.to_string())?;
    Ok(())
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
}
