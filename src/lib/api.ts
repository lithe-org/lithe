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

export interface ResourceUsage {
  cpuPercent: number;
  memoryPercent: number;
}

export function getResourceUsage(): Promise<ResourceUsage> {
  return invoke<ResourceUsage>("get_resource_usage");
}

/** 锚点是顶栏内的 CSS 逻辑坐标，Rust 侧按 DPI 换算成物理像素后再定位。 */
export interface LogicalRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export type PopupKind = "logo" | "control-center" | "split";

export function showPopup(anchor: LogicalRect, kind: PopupKind): Promise<void> {
  return invoke("show_popup", { anchor, kind });
}

export function hidePopup(): Promise<void> {
  return invoke("hide_popup");
}

/** 鼠标按下瞬间捕获控制目标；返回 null 表示当前窗口不可控制。 */
export function captureControlTarget(): Promise<ForegroundApp | null> {
  return invoke<ForegroundApp | null>("capture_control_target");
}

export function toggleTopmost(): Promise<void> {
  return invoke("toggle_topmost");
}

export function toggleMaximize(): Promise<void> {
  return invoke("toggle_maximize");
}

export function minimizeForeground(): Promise<void> {
  return invoke("minimize_foreground");
}

export type SplitLayout =
  | "left"
  | "right"
  | "top-left"
  | "top-right"
  | "bottom-left"
  | "bottom-right";

export function applySplit(layout: SplitLayout): Promise<void> {
  return invoke("apply_split", { layout });
}
