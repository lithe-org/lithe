use tauri::{AppHandle, Manager, PhysicalPosition, State, WebviewWindow};

use crate::window::placement::{place_popup, LogicalRect, Rect};
use crate::window::popup_pool::PopupPool;

const POPUP_GAP: i32 = 4;

#[tauri::command]
pub fn show_popup(
    app: AppHandle,
    window: WebviewWindow,
    pool: State<'_, PopupPool>,
    anchor: LogicalRect,
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
    popup.set_focus().map_err(|error| error.to_string())?;
    Ok(())
}

#[tauri::command]
pub fn hide_popup(pool: State<'_, PopupPool>, window: WebviewWindow) {
    pool.release(window);
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
