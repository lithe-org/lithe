use std::time::Duration;
use serde::{Deserialize, Serialize};
use tauri::{AppHandle, Emitter};
use windows_sys::Win32::System::Performance::{
    PdhAddEnglishCounterW, PdhCloseQuery, PdhCollectQueryData, PdhGetFormattedCounterValue,
    PdhOpenQueryW, PDH_FMT_COUNTERVALUE, PDH_FMT_DOUBLE, PDH_HCOUNTER, PDH_HQUERY,
};
use windows_sys::Win32::System::SystemInformation::{GlobalMemoryStatusEx, MEMORYSTATUSEX};

use crate::commands::EVENT_RESOURCE_TICK;

const CPU_COUNTER_PATH: &str = "\\Processor(_Total)\\% Processor Time";
const SAMPLE_INTERVAL: Duration = Duration::from_secs(1);

#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ResourceUsage {
    pub cpu_percent: f64,
    pub memory_percent: f64,
}

/// PDH 偶尔会给出越界值或 NaN，写进 UI 前必须夹回 0–100。
pub fn clamp_percent(value: f64) -> f64 {
    if value.is_nan() {
        return 0.0;
    }
    value.clamp(0.0, 100.0)
}

struct CpuCounter {
    query: PDH_HQUERY,
    counter: PDH_HCOUNTER,
}

impl CpuCounter {
    fn new() -> Option<Self> {
        unsafe {
            let mut query: PDH_HQUERY = std::ptr::null_mut();
            if PdhOpenQueryW(std::ptr::null(), 0, &mut query) != 0 {
                return None;
            }
            let path: Vec<u16> = CPU_COUNTER_PATH
                .encode_utf16()
                .chain(std::iter::once(0))
                .collect();
            let mut counter: PDH_HCOUNTER = std::ptr::null_mut();
            if PdhAddEnglishCounterW(query, path.as_ptr(), 0, &mut counter) != 0 {
                PdhCloseQuery(query);
                return None;
            }
            // 第一次采样只建立基线，结果要丢弃，否则首帧 CPU 恒为 0。
            PdhCollectQueryData(query);
            Some(Self { query, counter })
        }
    }

    fn sample(&mut self) -> Option<f64> {
        unsafe {
            if PdhCollectQueryData(self.query) != 0 {
                return None;
            }
            let mut value: PDH_FMT_COUNTERVALUE = std::mem::zeroed();
            let mut value_type = 0u32;
            if PdhGetFormattedCounterValue(
                self.counter,
                PDH_FMT_DOUBLE,
                &mut value_type,
                &mut value,
            ) != 0
            {
                return None;
            }
            if value.CStatus != 0 {
                return None;
            }
            Some(clamp_percent(value.Anonymous.doubleValue))
        }
    }
}

impl Drop for CpuCounter {
    fn drop(&mut self) {
        // 查询句柄只在采集线程里使用，关闭失败也无需重试。
        unsafe {
            PdhCloseQuery(self.query);
        }
    }
}

fn memory_usage() -> Option<f64> {
    let mut status: MEMORYSTATUSEX = unsafe { std::mem::zeroed() };
    status.dwLength = std::mem::size_of::<MEMORYSTATUSEX>() as u32;
    if unsafe { GlobalMemoryStatusEx(&mut status) } == 0 {
        return None;
    }
    // dwMemoryLoad 已经是 0–100 的百分比，不用再从字节数换算。
    Some(clamp_percent(status.dwMemoryLoad as f64))
}

pub fn start(app: AppHandle) {
    std::thread::spawn(move || {
        // 计数器不可用只影响资源图标，不能阻断状态栏启动。
        let Some(mut counter) = CpuCounter::new() else {
            return;
        };
        loop {
            if let (Some(cpu_percent), Some(memory_percent)) =
                (counter.sample(), memory_usage())
            {
                let _ = app.emit(
                    EVENT_RESOURCE_TICK,
                    ResourceUsage {
                        cpu_percent,
                        memory_percent,
                    },
                );
            }
            std::thread::sleep(SAMPLE_INTERVAL);
        }
    });
}

#[tauri::command]
pub fn get_resource_usage() -> Result<ResourceUsage, String> {
    let mut counter = CpuCounter::new().ok_or("CPU 计数器不可用")?;
    let cpu_percent = counter.sample().ok_or("CPU 采集失败")?;
    let memory_percent = memory_usage().ok_or("内存采集失败")?;
    Ok(ResourceUsage {
        cpu_percent,
        memory_percent,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clamp_percent_keeps_reported_values_in_range() {
        assert_eq!(clamp_percent(-3.0), 0.0);
        assert_eq!(clamp_percent(120.0), 100.0);
        assert_eq!(clamp_percent(42.5), 42.5);
        assert_eq!(clamp_percent(f64::NAN), 0.0);
        assert_eq!(clamp_percent(f64::INFINITY), 100.0);
        assert_eq!(clamp_percent(f64::NEG_INFINITY), 0.0);
    }
}
