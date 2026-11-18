use serde::Serialize;
use tauri::{AppHandle, Emitter, PhysicalPosition, PhysicalSize, State, WebviewWindow};

use crate::commands::EVENT_POPUP_SHOW;
use crate::window::placement::{place_popup, LogicalRect, Rect};
use crate::window::popup_pool::{PopupPool, POPUP_LABEL_PREFIX};

const POPUP_GAP: i32 = 4;

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct PopupShowPayload {
    kind: String,
    /// 控制中心打开时要聚焦的模块（network / volume / battery / resource / theme）。
    focus: Option<String>,
}

#[tauri::command]
pub fn show_popup(
    app: AppHandle,
    window: WebviewWindow,
    pool: State<'_, PopupPool>,
    anchor: LogicalRect,
    kind: String,
    focus: Option<String>,
) -> Result<(), String> {
    // 顶栏位于 (0,0)，逻辑锚点乘 scale 即屏幕物理坐标；M4 多屏时再补显示器偏移。
    let scale = window.scale_factor().map_err(|error| error.to_string())?;
    let anchor = Rect {
        x: (anchor.x * scale).round() as i32,
        y: (anchor.y * scale).round() as i32,
        width: (anchor.width * scale).round() as i32,
        height: (anchor.height * scale).round() as i32,
    };

    let popup = pool.acquire().ok_or("下拉窗口池已耗尽")?;
    let size = popup.outer_size().map_err(|error| error.to_string())?;
    let popup_rect = Rect {
        x: 0,
        y: 0,
        width: size.width as i32,
        height: size.height as i32,
    };
    let screen = screen_rect(&app).ok_or("无法获取主屏边界")?;
    let (x, y) = place_popup(anchor, popup_rect, screen, POPUP_GAP);

    popup
        .set_position(PhysicalPosition::new(x, y))
        .map_err(|error| error.to_string())?;
    popup.show().map_err(|error| error.to_string())?;
    // 池中页面上电即加载，先显示再注入内容不会丢事件。
    popup
        .emit(
            EVENT_POPUP_SHOW,
            PopupShowPayload { kind, focus },
        )
        .map_err(|error| error.to_string())?;
    popup.set_focus().map_err(|error| error.to_string())?;
    Ok(())
}

#[tauri::command]
pub fn hide_popup(pool: State<'_, PopupPool>, window: WebviewWindow) {
    // 只允许下拉窗口自释放；顶栏误调会把自己的窗口塞回池里。
    if !window.label().starts_with(POPUP_LABEL_PREFIX) {
        return;
    }
    pool.release(window);
}

#[tauri::command]
pub fn close_popup(pool: State<'_, PopupPool>) -> usize {
    pool.close_active()
}

#[tauri::command]
pub fn resize_popup(window: WebviewWindow, height: u32) -> Result<(), String> {
    // 宽度由窗口池固定，只按内容调整高度；调用方就是当前下拉窗口。
    window
        .set_size(PhysicalSize::new(
            crate::window::popup_pool::POPUP_WIDTH as u32,
            height.max(80),
        ))
        .map_err(|error| error.to_string())
}

fn screen_rect(app: &AppHandle) -> Option<Rect> {
    let monitor = app.primary_monitor().ok().flatten()?;
    let position = monitor.position();
    let size = monitor.size();
    Some(Rect {
        x: position.x,
        y: position.y,
        width: size.width as i32,
        height: size.height as i32,
    })
}
