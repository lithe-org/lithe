use serde::Serialize;
use tauri::{AppHandle, Emitter, PhysicalPosition, PhysicalSize, State, WebviewWindow};

use crate::commands::EVENT_POPUP_SHOW;
use crate::window::placement::{anchor_to_screen, place_popup, LogicalRect, Rect};
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
    let scale = window.scale_factor().map_err(|error| error.to_string())?;
    let origin = window
        .outer_position()
        .map_err(|error| error.to_string())?;
    let anchor = anchor_to_screen(anchor, scale, origin.x, origin.y);

    let popup = pool.acquire().ok_or("下拉窗口池已耗尽")?;
    let size = popup.outer_size().map_err(|error| error.to_string())?;
    let popup_rect = Rect {
        x: 0,
        y: 0,
        width: size.width as i32,
        height: size.height as i32,
    };
    // 越界翻转要按锚点所在屏计算，副屏点击不能用主屏的工作区。
    let screen = screen_for_anchor(anchor)
        .or_else(|| screen_rect(&app))
        .ok_or("无法获取显示器边界")?;
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

fn screen_for_anchor(anchor: Rect) -> Option<Rect> {
    // 复用 M4-01 的 Win32 枚举，坐标与顶栏/AppBar 使用同一套虚拟桌面系。
    let center_x = anchor.x + anchor.width / 2;
    let center_y = anchor.y + anchor.height / 2;
    let display = crate::window::monitors::enumerate()
        .ok()?
        .into_iter()
        .find(|display| {
            center_x >= display.bounds.left
                && center_x < display.bounds.right
                && center_y >= display.bounds.top
                && center_y < display.bounds.bottom
        })?;
    Some(Rect {
        x: display.bounds.left,
        y: display.bounds.top,
        width: display.bounds.width() as i32,
        height: display.bounds.height() as i32,
    })
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
