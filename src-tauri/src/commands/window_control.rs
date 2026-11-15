use std::ffi::c_void;
use std::sync::Mutex;
use windows_sys::Win32::Foundation::{HWND, RECT};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    GetForegroundWindow, GetWindowLongPtrW, IsZoomed, SetWindowPos, ShowWindow,
    SystemParametersInfoW, GWL_EXSTYLE, HWND_NOTOPMOST, HWND_TOPMOST, SPI_GETWORKAREA,
    SW_MAXIMIZE, SW_MINIMIZE, SW_RESTORE, SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE, SWP_NOZORDER,
    WS_EX_TOPMOST,
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
pub fn is_controllable_foreground() -> bool {
    is_controllable_window(unsafe { GetForegroundWindow() })
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

/// PRD 5.3.3 的六种分屏布局。
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SplitLayout {
    Left,
    Right,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

impl SplitLayout {
    pub fn parse(value: &str) -> Option<Self> {
        match value {
            "left" => Some(Self::Left),
            "right" => Some(Self::Right),
            "top-left" => Some(Self::TopLeft),
            "top-right" => Some(Self::TopRight),
            "bottom-left" => Some(Self::BottomLeft),
            "bottom-right" => Some(Self::BottomRight),
            _ => None,
        }
    }
}

/// 按工作区计算目标矩形；相邻布局共用中线，拼接处不能留缝。
pub fn split_rect(layout: SplitLayout, work: RECT) -> RECT {
    let half_width = (work.right - work.left) / 2;
    let half_height = (work.bottom - work.top) / 2;
    match layout {
        SplitLayout::Left => RECT {
            left: work.left,
            top: work.top,
            right: work.left + half_width,
            bottom: work.bottom,
        },
        SplitLayout::Right => RECT {
            left: work.left + half_width,
            top: work.top,
            right: work.right,
            bottom: work.bottom,
        },
        SplitLayout::TopLeft => RECT {
            left: work.left,
            top: work.top,
            right: work.left + half_width,
            bottom: work.top + half_height,
        },
        SplitLayout::TopRight => RECT {
            left: work.left + half_width,
            top: work.top,
            right: work.right,
            bottom: work.top + half_height,
        },
        SplitLayout::BottomLeft => RECT {
            left: work.left,
            top: work.top + half_height,
            right: work.left + half_width,
            bottom: work.bottom,
        },
        SplitLayout::BottomRight => RECT {
            left: work.left + half_width,
            top: work.top + half_height,
            right: work.right,
            bottom: work.bottom,
        },
    }
}

#[tauri::command]
pub fn apply_split(layout: String) -> Result<(), String> {
    let layout = SplitLayout::parse(&layout).ok_or("未知的分屏布局")?;
    let work = work_area().ok_or("无法获取工作区")?;
    let target = split_rect(layout, work);

    with_target(|hwnd| unsafe {
        // 最大化窗口直接 SetWindowPos 会被系统忽略，先还原再摆位置。
        if IsZoomed(hwnd) != 0 {
            ShowWindow(hwnd, SW_RESTORE);
        }
        let changed = SetWindowPos(
            hwnd,
            std::ptr::null_mut(),
            target.left,
            target.top,
            target.right - target.left,
            target.bottom - target.top,
            SWP_NOZORDER | SWP_NOACTIVATE,
        );
        if changed == 0 {
            return Err("应用分屏布局失败".to_string());
        }
        Ok(())
    })
}

fn work_area() -> Option<RECT> {
    // SPI_GETWORKAREA 已排除任务栏与 AppBar，直接就是可用区域。
    let mut rect: RECT = unsafe { std::mem::zeroed() };
    let ok = unsafe {
        SystemParametersInfoW(
            SPI_GETWORKAREA,
            0,
            &mut rect as *mut RECT as *mut c_void,
            0,
        )
    };
    (ok != 0).then_some(rect)
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

    const WORK: RECT = RECT {
        left: 0,
        top: 30,
        right: 1920,
        bottom: 1080,
    };

    #[test]
    fn half_layouts_split_the_work_area_without_a_gap() {
        let left = split_rect(SplitLayout::Left, WORK);
        let right = split_rect(SplitLayout::Right, WORK);

        assert_eq!((left.left, left.right), (0, 960));
        assert_eq!((right.left, right.right), (960, 1920));
        // 两半必须共用中线，留缝或多重叠都会露出桌面。
        assert_eq!(left.right, right.left);
        assert_eq!((left.top, left.bottom), (WORK.top, WORK.bottom));
    }

    #[test]
    fn quadrant_layouts_meet_at_the_center() {
        let top_left = split_rect(SplitLayout::TopLeft, WORK);
        let bottom_right = split_rect(SplitLayout::BottomRight, WORK);

        assert_eq!((top_left.left, top_left.top), (0, 30));
        assert_eq!((top_left.right, top_left.bottom), (960, 555));
        assert_eq!((bottom_right.left, bottom_right.top), (960, 555));
        assert_eq!((bottom_right.right, bottom_right.bottom), (1920, 1080));
    }

    #[test]
    fn layout_names_parse_to_the_expected_region() {
        // 前端与 Rust 用同一套字符串，拼错必须解析失败而不是落到默认布局。
        assert_eq!(SplitLayout::parse("left"), Some(SplitLayout::Left));
        assert_eq!(
            SplitLayout::parse("bottom-right"),
            Some(SplitLayout::BottomRight)
        );
        assert_eq!(SplitLayout::parse("middle"), None);
    }
}
