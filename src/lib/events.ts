// 与 src-tauri/src/commands/mod.rs 的常量逐字一致，由 api.test.ts 校验漂移。
// M1-06 起在本文件统一注册 listen，组件不直接订阅事件名字符串。
export const EVENTS = {
  foregroundChanged: "system:foreground-changed",
  statusTick: "system:status-tick",
  resourceTick: "system:resource-tick",
  wifiChanged: "system:wifi-changed",
  volumeChanged: "system:volume-changed",
  batteryChanged: "system:battery-changed",
} as const;
