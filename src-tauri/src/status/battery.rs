use std::time::Duration;
use serde::{Deserialize, Serialize};
use tauri::{AppHandle, Emitter};
use windows_sys::Win32::System::Power::{GetSystemPowerStatus, SYSTEM_POWER_STATUS};

use crate::commands::EVENT_BATTERY_CHANGED;

const BATTERY_FLAG_NO_BATTERY: u8 = 128;
const BATTERY_FLAG_UNKNOWN: u8 = 255;
const BATTERY_FLAG_CHARGING: u8 = 8;

#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BatteryStatus {
    pub present: bool,
    pub percent: u32,
    pub charging: bool,
}

impl BatteryStatus {
    fn absent() -> Self {
        Self {
            present: false,
            percent: 0,
            charging: false,
        }
    }
}

/// 128 表示无电池、255 表示状态未知，两者都按「台式机不显示」处理。
pub fn battery_present(flag: u8) -> bool {
    flag != BATTERY_FLAG_NO_BATTERY && flag != BATTERY_FLAG_UNKNOWN
}

/// 接入交流电源即视为充电/外接供电，充电中的电池位也可作为补充依据。
pub fn is_charging(ac_line_status: u8, flag: u8) -> bool {
    ac_line_status == 1 || flag & BATTERY_FLAG_CHARGING != 0
}

/// 把 C 结构解析成前端契约，纯函数便于覆盖台式机、未知电量等边界。
pub fn parse_power_status(status: &SYSTEM_POWER_STATUS) -> BatteryStatus {
    let present = battery_present(status.BatteryFlag);
    if !present {
        return BatteryStatus::absent();
    }
    BatteryStatus {
        present: true,
        percent: if status.BatteryLifePercent <= 100 {
            status.BatteryLifePercent as u32
        } else {
            0
        },
        charging: is_charging(status.ACLineStatus, status.BatteryFlag),
    }
}

#[tauri::command]
pub fn get_battery_status() -> BatteryStatus {
    let mut status: SYSTEM_POWER_STATUS = unsafe { std::mem::zeroed() };
    if unsafe { GetSystemPowerStatus(&mut status) } == 0 {
        return BatteryStatus::absent();
    }
    parse_power_status(&status)
}

pub fn start(app: AppHandle) {
    std::thread::spawn(move || {
        let mut previous = get_battery_status();
        loop {
            let current = get_battery_status();
            if current != previous {
                let _ = app.emit(EVENT_BATTERY_CHANGED, current);
                previous = current;
            }
            std::thread::sleep(Duration::from_secs(2));
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    fn raw(flag: u8, percent: u8, ac: u8) -> SYSTEM_POWER_STATUS {
        let mut status: SYSTEM_POWER_STATUS = unsafe { std::mem::zeroed() };
        status.BatteryFlag = flag;
        status.BatteryLifePercent = percent;
        status.ACLineStatus = ac;
        status
    }

    #[test]
    fn no_battery_or_unknown_flag_is_treated_as_desktop() {
        assert!(!battery_present(BATTERY_FLAG_NO_BATTERY));
        assert!(!battery_present(BATTERY_FLAG_UNKNOWN));
        assert_eq!(
            parse_power_status(&raw(BATTERY_FLAG_NO_BATTERY, 0, 1)),
            BatteryStatus::absent()
        );
    }

    #[test]
    fn laptop_state_maps_percent_and_charging() {
        let status = parse_power_status(&raw(8, 63, 1));
        assert_eq!(
            status,
            BatteryStatus {
                present: true,
                percent: 63,
                charging: true,
            }
        );
    }

    #[test]
    fn unknown_percentage_does_not_leak_255_into_the_ui() {
        let status = parse_power_status(&raw(1, 255, 0));
        assert!(status.present);
        assert_eq!(status.percent, 0);
        assert!(!status.charging);
    }

    #[test]
    fn charging_bit_counts_even_without_ac_line() {
        assert!(is_charging(0, BATTERY_FLAG_CHARGING));
    }
}
