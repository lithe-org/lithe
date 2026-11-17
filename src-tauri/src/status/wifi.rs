use std::ffi::c_void;
use std::time::Duration;
use serde::{Deserialize, Serialize};
use tauri::{AppHandle, Emitter};
use windows_sys::Win32::Foundation::HANDLE;
use windows_sys::Win32::NetworkManagement::WiFi::{
    wlan_intf_opcode_current_connection, wlan_interface_state_connected,
    wlan_interface_state_disconnected, WlanCloseHandle,
    WlanEnumInterfaces, WlanFreeMemory, WlanOpenHandle, WlanQueryInterface,
    WLAN_CONNECTION_ATTRIBUTES, WLAN_INTERFACE_INFO_LIST,
};

use crate::commands::EVENT_WIFI_CHANGED;

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WifiStatus {
    pub available: bool,
    pub connected: bool,
    pub ssid: String,
    pub signal: u32,
}

impl WifiStatus {
    fn unavailable() -> Self {
        Self {
            available: false,
            connected: false,
            ssid: String::new(),
            signal: 0,
        }
    }
}

/// WLAN 报告的信号质量已经是 0–100，只需兜底越界值。
pub fn clamp_signal(value: u32) -> u32 {
    value.min(100)
}

/// 把 WlanQueryInterface 的 C 结构转成前端契约；解析与 Win32 调用分离便于单测。
pub fn parse_connection(attributes: &WLAN_CONNECTION_ATTRIBUTES) -> WifiStatus {
    if attributes.isState != wlan_interface_state_connected {
        return WifiStatus {
            available: true,
            connected: false,
            ssid: String::new(),
            signal: 0,
        };
    }
    WifiStatus {
        available: true,
        connected: true,
        ssid: utf16_prefix(&attributes.strProfileName),
        signal: clamp_signal(attributes.wlanAssociationAttributes.wlanSignalQuality),
    }
}

fn utf16_prefix(chars: &[u16]) -> String {
    let length = chars
        .iter()
        .position(|&unit| unit == 0)
        .unwrap_or(chars.len());
    String::from_utf16_lossy(&chars[..length])
}

#[tauri::command]
pub fn get_wifi_status() -> WifiStatus {
    unsafe {
        let mut negotiated = 0u32;
        let mut handle: HANDLE = std::ptr::null_mut();
        // 没有 WLAN 服务或适配器时打开失败，按「不可用」处理，不能让状态栏报错。
        if WlanOpenHandle(2, std::ptr::null(), &mut negotiated, &mut handle) != 0 {
            return WifiStatus::unavailable();
        }

        let mut list: *mut WLAN_INTERFACE_INFO_LIST = std::ptr::null_mut();
        if WlanEnumInterfaces(handle, std::ptr::null(), &mut list) != 0 {
            WlanCloseHandle(handle, std::ptr::null());
            return WifiStatus::unavailable();
        }

        let mut result = WifiStatus::unavailable();
        if !list.is_null() && (*list).dwNumberOfItems > 0 {
            let interface = (*list).InterfaceInfo.as_ptr();
            let mut size = 0u32;
            let mut data: *mut c_void = std::ptr::null_mut();
            let ok = WlanQueryInterface(
                handle,
                &(*interface).InterfaceGuid,
                wlan_intf_opcode_current_connection,
                std::ptr::null(),
                &mut size,
                &mut data,
                std::ptr::null_mut(),
            );
            if ok == 0 && !data.is_null() {
                result = parse_connection(&*(data as *const WLAN_CONNECTION_ATTRIBUTES));
                WlanFreeMemory(data);
            }
        }

        if !list.is_null() {
            WlanFreeMemory(list as *const c_void);
        }
        WlanCloseHandle(handle, std::ptr::null());
        result
    }
}

pub fn start(app: AppHandle) {
    std::thread::spawn(move || {
        let mut previous = WifiStatus::unavailable();
        loop {
            let current = get_wifi_status();
            // 只在状态真的变化时推送，避免每秒唤醒整个前端。
            if current != previous {
                let _ = app.emit(EVENT_WIFI_CHANGED, current.clone());
                previous = current;
            }
            std::thread::sleep(Duration::from_secs(2));
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn connected_attributes_map_to_the_frontend_contract() {
        let mut attributes: WLAN_CONNECTION_ATTRIBUTES = unsafe { std::mem::zeroed() };
        attributes.isState = wlan_interface_state_connected;
        attributes.strProfileName[..4].copy_from_slice(&['H' as u16, 'o' as u16, 'm' as u16, 'e' as u16]);
        attributes.wlanAssociationAttributes.wlanSignalQuality = 78;

        assert_eq!(
            parse_connection(&attributes),
            WifiStatus {
                available: true,
                connected: true,
                ssid: "Home".to_string(),
                signal: 78,
            }
        );
    }

    #[test]
    fn disconnected_attributes_do_not_keep_a_stale_ssid() {
        let mut attributes: WLAN_CONNECTION_ATTRIBUTES = unsafe { std::mem::zeroed() };
        attributes.isState = wlan_interface_state_connected;
        attributes.strProfileName[..4].copy_from_slice(&['H' as u16, 'o' as u16, 'm' as u16, 'e' as u16]);
        attributes.isState = wlan_interface_state_disconnected;

        assert_eq!(
            parse_connection(&attributes),
            WifiStatus {
                available: true,
                connected: false,
                ssid: String::new(),
                signal: 0,
            }
        );
    }

    #[test]
    fn clamp_signal_caps_out_of_range_values() {
        assert_eq!(clamp_signal(120), 100);
        assert_eq!(clamp_signal(45), 45);
    }
}
