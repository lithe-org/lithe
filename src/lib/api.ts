import { invoke } from "@tauri-apps/api/core";

/** 与 src-tauri/src/commands/system.rs 的 ForegroundApp 字段逐字对齐。 */
export interface ForegroundApp {
  name: string;
  executable: string;
  controllable: boolean;
}

/** 与 src-tauri/src/commands/system.rs 的 SystemStatus 字段逐字对齐。 */
export interface SystemStatus {
  cpuPercent: number;
  memoryPercent: number;
  volume: number;
  muted: boolean;
  batteryPercent: number | null;
  wifiConnected: boolean;
  time: string;
}

// 命令名必须与 Rust 侧 #[tauri::command] 函数名一致，漏注册只在运行时暴露。
export function getForegroundApp(): Promise<ForegroundApp> {
  return invoke<ForegroundApp>("get_foreground_app");
}

export function getSystemStatus(): Promise<SystemStatus> {
  return invoke<SystemStatus>("get_system_status");
}
