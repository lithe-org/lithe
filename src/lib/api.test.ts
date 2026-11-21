import { invoke } from "@tauri-apps/api/core";
import { readFileSync } from "node:fs";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  applySplit,
  closePopup,
  getBatteryStatus,
  openNotificationCenter,
  getForegroundApp,
  getResourceUsage,
  getSystemStatus,
  getThemeInfo,
  getPopupState,
  showSettingsWindow,
  getFullscreenAutohide,
  setFullscreenAutohide,
  getVolume,
  setVolume,
  toggleMute,
  getWifiStatus,
  hidePopup,
  resizePopup,
  showPopup,
} from "./api";
import { EVENTS } from "./events";

const rustEvents = readFileSync("src-tauri/src/commands/mod.rs", "utf8");
const rustCommands = readFileSync("src-tauri/src/commands/system.rs", "utf8");
const rustPopupCommands = readFileSync("src-tauri/src/commands/popup.rs", "utf8");
const rustResource = readFileSync("src-tauri/src/status/resource.rs", "utf8");
const rustWifi = readFileSync("src-tauri/src/status/wifi.rs", "utf8");
const rustAudio = readFileSync("src-tauri/src/status/audio.rs", "utf8");
const rustBattery = readFileSync("src-tauri/src/status/battery.rs", "utf8");
const rustNotification = readFileSync(
  "src-tauri/src/commands/notification.rs",
  "utf8",
);
const rustWindowControl = readFileSync(
  "src-tauri/src/commands/window_control.rs",
  "utf8",
);
const rustSettings = readFileSync("src-tauri/src/commands/settings.rs", "utf8");

function rustEventName(constant: string): string {
  const match = rustEvents.match(new RegExp(`pub const ${constant}: &str = "([^"]+)"`));
  if (!match) {
    throw new Error(`Rust 侧缺少事件常量 ${constant}`);
  }
  return match[1];
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("前后端通信契约", () => {
  it("getForegroundApp 调用 Rust 同名命令，拼写漂移要在测试里暴露", async () => {
    const app = { name: "记事本", executable: "notepad.exe", controllable: true };
    vi.mocked(invoke).mockResolvedValue(app);

    await expect(getForegroundApp()).resolves.toEqual(app);
    expect(invoke).toHaveBeenCalledWith("get_foreground_app");
    expect(invoke).toHaveBeenCalledTimes(1);
  });

  it("getSystemStatus 调用 Rust 同名命令，且不附加多余参数", async () => {
    const status = {
      cpuPercent: 12,
      memoryPercent: 34,
      volume: 50,
      muted: false,
      batteryPercent: null,
      wifiConnected: false,
      time: "12:00",
    };
    vi.mocked(invoke).mockResolvedValue(status);

    await expect(getSystemStatus()).resolves.toEqual(status);
    expect(invoke).toHaveBeenCalledWith("get_system_status");
    expect(invoke).toHaveBeenCalledTimes(1);
  });

  it("showPopup / hidePopup 调用 Rust 同名命令并透传锚点", async () => {
    vi.mocked(invoke).mockResolvedValue(undefined);
    const anchor = { x: 12, y: 0, width: 20, height: 30 };

    await showPopup(anchor, "logo");
    expect(invoke).toHaveBeenCalledWith("show_popup", {
      anchor,
      kind: "logo",
      focus: null,
    });

    await hidePopup();
    expect(invoke).toHaveBeenCalledWith("hide_popup");
  });

  it("getResourceUsage 读取 Rust 同名命令的驼峰字段", async () => {
    vi.mocked(invoke).mockResolvedValue({ cpuPercent: 12.5, memoryPercent: 34 });

    const usage = await getResourceUsage();

    expect(invoke).toHaveBeenCalledWith("get_resource_usage");
    expect(usage).toEqual({ cpuPercent: 12.5, memoryPercent: 34 });
  });

  it("getWifiStatus 读取 Rust 同名命令的驼峰字段", async () => {
    vi.mocked(invoke).mockResolvedValue({
      available: true,
      connected: true,
      ssid: "Home",
      signal: 72,
    });

    const status = await getWifiStatus();

    expect(invoke).toHaveBeenCalledWith("get_wifi_status");
    expect(status.available).toBe(true);
    expect(status.signal).toBe(72);
  });

  it("音量命令读写系统音量与静音状态", async () => {
    vi.mocked(invoke).mockResolvedValue({ level: 55, muted: false });

    expect(await getVolume()).toEqual({ level: 55, muted: false });
    expect(invoke).toHaveBeenCalledWith("get_volume");

    await setVolume(72);
    expect(invoke).toHaveBeenCalledWith("set_volume", { level: 72 });

    await toggleMute();
    expect(invoke).toHaveBeenCalledWith("toggle_mute");
  });

  it("通知入口先收面板再发送 Win+N", async () => {
    vi.mocked(invoke).mockResolvedValue(1);

    expect(await closePopup()).toBe(1);
    expect(invoke).toHaveBeenCalledWith("close_popup");

    await openNotificationCenter();
    expect(invoke).toHaveBeenCalledWith("open_notification_center");
  });

  it("getThemeInfo 读取系统明暗与强调色", async () => {
    vi.mocked(invoke).mockResolvedValue({ dark: true, accent: "#AABBCC" });

    expect(await getThemeInfo()).toEqual({ dark: true, accent: "#AABBCC" });
    expect(invoke).toHaveBeenCalledWith("get_theme_info");
  });

  it("showSettingsWindow 调用 Rust 统一命令而不是前端枚举窗口", async () => {
    await showSettingsWindow();

    expect(invoke).toHaveBeenCalledWith("show_settings_window");
    expect(rustSettings).toContain("pub fn show_settings_window(");
  });

  it("getPopupState 提供面板内容拉取兜底", async () => {
    vi.mocked(invoke).mockResolvedValue({ kind: "logo" });

    expect(await getPopupState()).toEqual({ kind: "logo" });
    expect(invoke).toHaveBeenCalledWith("get_popup_state");
    expect(rustPopupCommands).toContain("pub fn get_popup_state(");
  });

  it("全屏自动隐藏开关调用 Rust 同名命令", async () => {
    vi.mocked(invoke).mockResolvedValue(true);

    await setFullscreenAutohide(false);
    expect(invoke).toHaveBeenCalledWith("set_fullscreen_autohide", {
      enabled: false,
    });

    expect(await getFullscreenAutohide()).toBe(true);
    expect(invoke).toHaveBeenCalledWith("get_fullscreen_autohide");
  });

  it("resizePopup 按内容高度调整面板，宽度由 Rust 固定", async () => {
    await resizePopup(320);
    expect(invoke).toHaveBeenCalledWith("resize_popup", { height: 320 });
  });

  it("getBatteryStatus 读取 Rust 同名命令的驼峰字段", async () => {
    vi.mocked(invoke).mockResolvedValue({
      present: true,
      percent: 77,
      charging: false,
    });

    expect(await getBatteryStatus()).toEqual({
      present: true,
      percent: 77,
      charging: false,
    });
    expect(invoke).toHaveBeenCalledWith("get_battery_status");
  });

  it("applySplit 透传布局名，由 Rust 计算目标矩形", async () => {
    await applySplit("bottom-right");
    expect(invoke).toHaveBeenCalledWith("apply_split", {
      layout: "bottom-right",
    });
  });

  it("前端命令名必须能在 Rust 侧找到同名 command 函数", () => {
    expect(rustCommands).toContain("pub fn get_foreground_app()");
    expect(rustCommands).toContain("pub fn get_system_status()");
    expect(rustPopupCommands).toContain("pub fn show_popup(");
    expect(rustPopupCommands).toContain("pub fn hide_popup(");
    expect(rustPopupCommands).toContain("pub fn resize_popup(");
    expect(rustPopupCommands).toContain("pub fn close_popup(");
    expect(rustSettings).toContain("pub fn set_fullscreen_autohide(");
    expect(rustSettings).toContain("pub fn set_bar_height(");
    expect(rustNotification).toContain("pub fn open_notification_center()");
    expect(rustWindowControl).toContain("pub fn toggle_topmost()");
    expect(rustWindowControl).toContain("pub fn toggle_maximize()");
    expect(rustWindowControl).toContain("pub fn minimize_foreground()");
    expect(rustWindowControl).toContain("pub fn apply_split(");
    expect(rustResource).toContain("pub fn get_resource_usage()");
    expect(rustWifi).toContain("pub fn get_wifi_status()");
    expect(rustAudio).toContain("pub fn get_volume()");
    expect(rustAudio).toContain("pub fn set_volume(");
    expect(rustAudio).toContain("pub fn toggle_mute()");
    expect(rustBattery).toContain("pub fn get_battery_status()");
  });

  it("前端事件名必须与 Rust 常量逐字一致，否则订阅永远收不到", () => {
    expect(EVENTS.foregroundChanged).toBe(rustEventName("EVENT_FOREGROUND_CHANGED"));
    expect(EVENTS.statusTick).toBe(rustEventName("EVENT_STATUS_TICK"));
    expect(EVENTS.resourceTick).toBe(rustEventName("EVENT_RESOURCE_TICK"));
    expect(EVENTS.wifiChanged).toBe(rustEventName("EVENT_WIFI_CHANGED"));
    expect(EVENTS.volumeChanged).toBe(rustEventName("EVENT_VOLUME_CHANGED"));
    expect(EVENTS.batteryChanged).toBe(rustEventName("EVENT_BATTERY_CHANGED"));
    expect(EVENTS.fullscreenChanged).toBe(
      rustEventName("EVENT_FULLSCREEN_CHANGED"),
    );
    expect(EVENTS.themeChanged).toBe(rustEventName("EVENT_THEME_CHANGED"));
    expect(EVENTS.popupShow).toBe(rustEventName("EVENT_POPUP_SHOW"));
  });
});
