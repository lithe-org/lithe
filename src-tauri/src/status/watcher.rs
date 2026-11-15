use std::sync::OnceLock;
use tauri::{AppHandle, Emitter};
use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::UI::Accessibility::{
    SetWinEventHook, UnhookWinEvent, HWINEVENTHOOK,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    DispatchMessageW, GetMessageW, TranslateMessage, EVENT_SYSTEM_FOREGROUND, MSG,
    WINEVENT_OUTOFCONTEXT, WINEVENT_SKIPOWNPROCESS,
};

use crate::commands::EVENT_FOREGROUND_CHANGED;

static APP_HANDLE: OnceLock<AppHandle> = OnceLock::new();

pub fn start(app: AppHandle) {
    // 重复启动只保留第一个句柄，避免多套 hook 重复推送同一事件。
    if APP_HANDLE.set(app).is_err() {
        return;
    }
    std::thread::spawn(run_hook_loop);
}

fn run_hook_loop() {
    unsafe {
        let hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            std::ptr::null_mut(),
            Some(on_foreground_changed),
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS,
        );
        if hook.is_null() {
            return;
        }
        // OUTOFCONTEXT 回调只在设置 hook 的线程泵消息时投递，必须自带消息循环。
        let mut message: MSG = std::mem::zeroed();
        while GetMessageW(&mut message, std::ptr::null_mut(), 0, 0) > 0 {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        UnhookWinEvent(hook);
    }
}

unsafe extern "system" fn on_foreground_changed(
    _hook: HWINEVENTHOOK,
    _event: u32,
    _hwnd: HWND,
    _id_object: i32,
    _id_child: i32,
    _id_event_thread: u32,
    _dwms_event_time: u32,
) {
    // 跨 FFI 边界不能 panic；句柄未就绪或 emit 失败都只是丢弃本次通知。
    if let Some(app) = APP_HANDLE.get() {
        let payload = crate::status::foreground::capture_app();
        let _ = app.emit(EVENT_FOREGROUND_CHANGED, payload);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn callback_is_safe_before_app_handle_is_ready() {
        // 启动早期 hook 可能先于 AppHandle 触发，空句柄必须静默返回。
        unsafe {
            on_foreground_changed(
                std::ptr::null_mut(),
                EVENT_SYSTEM_FOREGROUND,
                std::ptr::null_mut(),
                0,
                0,
                0,
                0,
            );
        }
    }
}
