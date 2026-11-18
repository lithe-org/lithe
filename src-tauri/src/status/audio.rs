use std::time::Duration;
use serde::{Deserialize, Serialize};
use tauri::{AppHandle, Emitter};
use windows::Win32::Media::Audio::Endpoints::IAudioEndpointVolume;
use windows::Win32::Media::Audio::{
    eCommunications, eRender, IMMDeviceEnumerator, MMDeviceEnumerator,
};
use windows::Win32::System::Com::{
    CoCreateInstance, CoInitializeEx, CoUninitialize, CLSCTX_ALL, COINIT_MULTITHREADED,
};

use crate::commands::EVENT_VOLUME_CHANGED;

#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VolumeStatus {
    pub level: u32,
    pub muted: bool,
}

/// Core Audio 用 0.0–1.0 的标量，边界与 NaN 都要兜住。
pub fn clamp_volume(value: f32) -> f32 {
    if value.is_nan() {
        return 0.0;
    }
    value.clamp(0.0, 1.0)
}

fn with_endpoint<T>(action: impl FnOnce(&IAudioEndpointVolume) -> T) -> Option<T> {
    unsafe {
        // COM 必须每线程初始化；已初始化时返回失败，此时不能由我们反初始化。
        let owns_init = CoInitializeEx(None, COINIT_MULTITHREADED).is_ok();
        let result = (|| {
            let enumerator: IMMDeviceEnumerator =
                CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL).ok()?;
            let device = enumerator
                .GetDefaultAudioEndpoint(eRender, eCommunications)
                .ok()?;
            let endpoint: IAudioEndpointVolume = device.Activate(CLSCTX_ALL, None).ok()?;
            Some(action(&endpoint))
        })();
        if owns_init {
            CoUninitialize();
        }
        result
    }
}

fn read_status() -> Option<VolumeStatus> {
    with_endpoint(|endpoint| unsafe {
        let level = endpoint.GetMasterVolumeLevelScalar().ok()?;
        let muted = endpoint.GetMute().ok()?.as_bool();
        Some(VolumeStatus {
            level: (clamp_volume(level) * 100.0).round() as u32,
            muted,
        })
    })
    .flatten()
}

#[tauri::command]
pub fn get_volume() -> Result<VolumeStatus, String> {
    read_status().ok_or("无法读取系统音量".to_string())
}

#[tauri::command]
pub fn set_volume(level: u32) -> Result<(), String> {
    let normalized = clamp_volume(level.min(100) as f32 / 100.0);
    with_endpoint(|endpoint| unsafe {
        endpoint
            .SetMasterVolumeLevelScalar(normalized, std::ptr::null())
            .ok()
    })
    .flatten()
    .ok_or("设置音量失败".to_string())
}

#[tauri::command]
pub fn toggle_mute() -> Result<(), String> {
    with_endpoint(|endpoint| unsafe {
        let target = !endpoint.GetMute().ok()?.as_bool();
        endpoint.SetMute(target, std::ptr::null()).ok()
    })
    .flatten()
    .ok_or("切换静音失败".to_string())
}

pub fn start(app: AppHandle) {
    std::thread::spawn(move || {
        let mut previous = read_status();
        loop {
            let current = read_status();
            // 只在真的变化时推送；每线程各自初始化 COM，不会被前端刷新影响。
            if current != previous {
                if let Some(status) = current {
                    let _ = app.emit(EVENT_VOLUME_CHANGED, status);
                }
                previous = current;
            }
            std::thread::sleep(Duration::from_millis(500));
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clamp_volume_keeps_scalar_in_range() {
        assert_eq!(clamp_volume(-0.2), 0.0);
        assert_eq!(clamp_volume(1.4), 1.0);
        assert_eq!(clamp_volume(0.42), 0.42);
        assert_eq!(clamp_volume(f32::NAN), 0.0);
    }
}
