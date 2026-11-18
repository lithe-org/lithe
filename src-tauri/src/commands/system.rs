use serde::{Deserialize, Serialize};

/// 前台应用名契约。M1-05 起由 Win32 采集填充，此处先返回 mock 数据。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ForegroundApp {
    pub name: String,
    pub executable: String,
    pub controllable: bool,
}

/// 系统状态快照契约。M3 起由各采集模块填充，此处先返回 mock 数据。
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SystemStatus {
    pub cpu_percent: f32,
    pub memory_percent: f32,
    pub volume: u8,
    pub muted: bool,
    pub battery_percent: Option<u8>,
    pub wifi_connected: bool,
    pub time: String,
}

#[tauri::command]
pub fn get_foreground_app() -> ForegroundApp {
    crate::status::foreground::capture_app()
}

#[tauri::command]
pub fn get_system_status() -> SystemStatus {
    SystemStatus {
        cpu_percent: 0.0,
        memory_percent: 0.0,
        volume: 50,
        muted: false,
        battery_percent: None,
        wifi_connected: false,
        time: "00:00".to_string(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn mock_commands_stay_within_contract_bounds() {
        // 前端直接把百分比与音量渲染到状态栏，越界值会变成可见的脏数据。
        // executable 依赖真实前台窗口，无桌面的测试环境允许为空；应用名必须始终有兜底。
        let app = get_foreground_app();
        assert!(!app.name.is_empty());

        let status = get_system_status();
        assert!((0.0..=100.0).contains(&status.cpu_percent));
        assert!((0.0..=100.0).contains(&status.memory_percent));
        assert!(status.volume <= 100);
        assert_eq!(status.time.len(), 5, "时间契约固定为 HH:mm");
    }

    #[test]
    fn serialized_fields_use_camel_case() {
        // snake_case 字段经 rename_all 后必须变成前端约定的驼峰，否则前端读到 undefined。
        let status = serde_json::to_value(get_system_status()).expect("状态应可序列化");
        assert!(status.get("cpuPercent").is_some());
        assert!(status.get("memoryPercent").is_some());
        assert!(status.get("wifiConnected").is_some());
        assert!(status.get("batteryPercent").is_some());
        assert!(status.get("cpu_percent").is_none());

        let app = serde_json::to_value(ForegroundApp {
            name: "应用".to_string(),
            executable: String::new(),
            controllable: true,
        })
        .expect("应用名应可序列化");
        assert!(app.get("controllable").is_some());
    }

    #[test]
    fn commands_must_be_registered_in_invoke_handler() {
        // 编译器抓不到命令漏注册，只有运行时 invoke 才报错，所以把注册列表钉进测试。
        let lib_source = include_str!("../lib.rs");
        let handler = lib_source
            .split("generate_handler![")
            .nth(1)
            .expect("lib.rs 缺少 invoke_handler 注册")
            .split(']')
            .next()
            .expect("注册列表未闭合");

        assert!(handler.contains("get_foreground_app"));
        assert!(handler.contains("get_system_status"));
        assert!(handler.contains("show_popup"));
        assert!(handler.contains("hide_popup"));
        assert!(handler.contains("resize_popup"));
        assert!(handler.contains("close_popup"));
        assert!(handler.contains("open_notification_center"));
        assert!(handler.contains("capture_control_target"));
        assert!(handler.contains("is_controllable_foreground"));
        assert!(handler.contains("toggle_topmost"));
        assert!(handler.contains("toggle_maximize"));
        assert!(handler.contains("minimize_foreground"));
        assert!(handler.contains("apply_split"));
    }
}
