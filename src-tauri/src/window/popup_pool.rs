use std::sync::Mutex;
use tauri::{AppHandle, Manager, WebviewUrl, WebviewWindow, WebviewWindowBuilder, WindowEvent};

const POOL_SIZE: usize = 2;
pub const POPUP_WIDTH: f64 = 320.0;
pub const POPUP_LABEL_PREFIX: &str = "popup-";
const POPUP_HEIGHT: f64 = 400.0;

/// 与 UI 无关的槽位管理，便于脱离桌面环境验证「取出 / 归还 / 空池」语义。
pub struct PopupSlots<T> {
    idle: Mutex<Vec<T>>,
}

impl<T> PopupSlots<T> {
    pub fn new(items: Vec<T>) -> Self {
        Self {
            idle: Mutex::new(items),
        }
    }

    pub fn acquire(&self) -> Option<T> {
        self.idle.lock().ok()?.pop()
    }

    pub fn release(&self, item: T) {
        if let Ok(mut idle) = self.idle.lock() {
            idle.push(item);
        }
    }

    pub fn contains(&self, predicate: impl Fn(&T) -> bool) -> bool {
        self.idle
            .lock()
            .map(|idle| idle.iter().any(predicate))
            .unwrap_or(false)
    }

    pub fn len(&self) -> usize {
        self.idle.lock().map(|idle| idle.len()).unwrap_or(0)
    }
}

/// 下拉窗口池：启动时预创建，点击时复用，避免每次新建 WebView 的明显延迟。
pub struct PopupPool {
    slots: PopupSlots<WebviewWindow>,
    /// 正在显示的面板；顶栏点通知/时间时需要显式收起它们（顶栏不抢焦点，失焦事件不一定触发）。
    active: Mutex<Vec<WebviewWindow>>,
}

impl PopupPool {
    pub fn new(app: &AppHandle) -> tauri::Result<Self> {
        let mut windows = Vec::with_capacity(POOL_SIZE);
        for index in 0..POOL_SIZE {
            windows.push(create_popup(app, index)?);
        }
        Ok(Self {
            slots: PopupSlots::new(windows),
            active: Mutex::new(Vec::new()),
        })
    }

    pub fn acquire(&self) -> Option<WebviewWindow> {
        // 只取窗口不显示：调用方先定位再 show，避免面板先闪到上一次的位置。
        let window = self.slots.acquire()?;
        if let Ok(mut active) = self.active.lock() {
            active.push(window.clone());
        }
        Some(window)
    }

    pub fn release(&self, window: WebviewWindow) {
        let _ = window.hide();
        if let Ok(mut active) = self.active.lock() {
            active.retain(|item| item.label() != window.label());
        }
        // 失焦与 Esc 可能连续触发同一窗口，按 label 去重避免重复入池。
        if !self.slots.contains(|idle| idle.label() == window.label()) {
            self.slots.release(window);
        }
    }

    /// 收起所有正在显示的面板，返回被收起的数量。
    pub fn close_active(&self) -> usize {
        let windows = self
            .active
            .lock()
            .map(|mut active| std::mem::take(&mut *active))
            .unwrap_or_default();
        let count = windows.len();
        for window in windows {
            self.release(window);
        }
        count
    }
}

fn create_popup(app: &AppHandle, index: usize) -> tauri::Result<WebviewWindow> {
    let label = format!("{POPUP_LABEL_PREFIX}{index}");
    let window = WebviewWindowBuilder::new(app, &label, WebviewUrl::App("index.html".into()))
        .title("Lithe Popup")
        .inner_size(POPUP_WIDTH, POPUP_HEIGHT)
        .visible(false)
        .decorations(false)
        .transparent(true)
        .always_on_top(true)
        .skip_taskbar(true)
        .focused(false)
        .resizable(false)
        .shadow(false)
        .build()?;

    // 失焦即关闭：点击面板外部或切到其他窗口都应收回，并归还池复用。
    let app_handle = app.clone();
    let owned = window.clone();
    window.on_window_event(move |event| {
        if let WindowEvent::Focused(false) = event {
            if let Some(pool) = app_handle.try_state::<PopupPool>() {
                pool.release(owned.clone());
            }
        }
    });

    Ok(window)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn acquire_takes_one_slot_and_empty_pool_returns_none() {
        let slots = PopupSlots::new(vec![1, 2]);
        assert_eq!(slots.len(), 2);
        assert_eq!(slots.acquire(), Some(2));
        assert_eq!(slots.acquire(), Some(1));
        assert_eq!(slots.acquire(), None);
    }

    #[test]
    fn release_puts_the_slot_back_for_reuse() {
        let slots = PopupSlots::new(vec![1]);
        let taken = slots.acquire().expect("池中应有一个槽位");
        assert_eq!(slots.len(), 0);
        slots.release(taken);
        assert_eq!(slots.len(), 1);
        assert_eq!(slots.acquire(), Some(1));
    }

    #[test]
    fn contains_reports_slots_by_label() {
        // PopupPool 靠 contains 去重，避免失焦与 Esc 连续归还同一窗口。
        let slots: PopupSlots<&str> = PopupSlots::new(vec!["popup-0"]);
        assert!(slots.contains(|label| *label == "popup-0"));
        assert!(!slots.contains(|label| *label == "popup-1"));
    }
}
