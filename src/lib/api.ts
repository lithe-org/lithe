import { invoke } from "@tauri-apps/api/core";
import type { ThemeInfo } from "../stores/theme";

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

export interface WifiStatus {
  available: boolean;
  connected: boolean;
  ssid: string;
  signal: number;
}

export function getWifiStatus(): Promise<WifiStatus> {
  return invoke<WifiStatus>("get_wifi_status");
}

export interface WifiNetwork {
  ssid: string;
  signal: number;
  connected: boolean;
  secure: boolean;
}

export function listWifiNetworks(): Promise<WifiNetwork[]> {
  return invoke<WifiNetwork[]>("list_wifi_networks");
}

export interface VolumeStatus {
  level: number;
  muted: boolean;
}

export function getVolume(): Promise<VolumeStatus> {
  return invoke<VolumeStatus>("get_volume");
}

export function setVolume(level: number): Promise<void> {
  return invoke("set_volume", { level });
}

export function toggleMute(): Promise<void> {
  return invoke("toggle_mute");
}

export interface BatteryStatus {
  present: boolean;
  percent: number;
  charging: boolean;
}

export function getBatteryStatus(): Promise<BatteryStatus> {
  return invoke<BatteryStatus>("get_battery_status");
}

/** 锚点是顶栏内的 CSS 逻辑坐标，Rust 侧按 DPI 换算成物理像素后再定位。 */
export interface LogicalRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export type PopupKind = "logo" | "control-center" | "split";

export function showPopup(
  anchor: LogicalRect,
  kind: PopupKind,
  focus?: string,
): Promise<void> {
  return invoke("show_popup", { anchor, kind, focus: focus ?? null });
}

export function resizePopup(height: number): Promise<void> {
  return invoke("resize_popup", { height });
}

/** 收起所有正在显示的下拉面板，返回被收起的数量。 */
export function closePopup(): Promise<number> {
  return invoke<number>("close_popup");
}

export function openNotificationCenter(): Promise<void> {
  return invoke("open_notification_center");
}

export function getThemeInfo(): Promise<ThemeInfo> {
  return invoke<ThemeInfo>("get_theme_info");
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
