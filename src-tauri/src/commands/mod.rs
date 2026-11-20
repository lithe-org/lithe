pub mod notification;
pub mod popup;
pub mod power;
pub mod system;
pub mod theme;
pub mod window_control;

// 事件名是前后端共享契约，前端 src/lib/events.ts 逐字对齐；改名必须两边同步。
pub const EVENT_FOREGROUND_CHANGED: &str = "system:foreground-changed";
pub const EVENT_STATUS_TICK: &str = "system:status-tick";
pub const EVENT_RESOURCE_TICK: &str = "system:resource-tick";
pub const EVENT_WIFI_CHANGED: &str = "system:wifi-changed";
pub const EVENT_VOLUME_CHANGED: &str = "system:volume-changed";
pub const EVENT_BATTERY_CHANGED: &str = "system:battery-changed";
pub const EVENT_FULLSCREEN_CHANGED: &str = "system:fullscreen-changed";
pub const EVENT_THEME_CHANGED: &str = "system:theme-changed";

/// 下拉窗口复用时用它注入内容类型（logo / control-center / split）。
pub const EVENT_POPUP_SHOW: &str = "popup:show";

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn event_names_share_the_system_namespace() {
        // 订阅端按完整字符串匹配，命名空间漂移会让事件永远收不到。
        for event in [
            EVENT_FOREGROUND_CHANGED,
            EVENT_STATUS_TICK,
            EVENT_RESOURCE_TICK,
            EVENT_WIFI_CHANGED,
            EVENT_VOLUME_CHANGED,
            EVENT_BATTERY_CHANGED,
        ] {
            assert!(event.starts_with("system:"), "事件名越出命名空间: {event}");
        }
    }
}
