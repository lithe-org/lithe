import { invoke } from "@tauri-apps/api/core";
import { readFileSync } from "node:fs";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  applySplit,
  getForegroundApp,
  getSystemStatus,
  hidePopup,
  showPopup,
} from "./api";
import { EVENTS } from "./events";

const rustEvents = readFileSync("src-tauri/src/commands/mod.rs", "utf8");
const rustCommands = readFileSync("src-tauri/src/commands/system.rs", "utf8");
const rustPopupCommands = readFileSync("src-tauri/src/commands/popup.rs", "utf8");
const rustWindowControl = readFileSync(
  "src-tauri/src/commands/window_control.rs",
  "utf8",
);

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
    expect(invoke).toHaveBeenCalledWith("show_popup", { anchor, kind: "logo" });

    await hidePopup();
    expect(invoke).toHaveBeenCalledWith("hide_popup");
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
    expect(rustWindowControl).toContain("pub fn toggle_topmost()");
    expect(rustWindowControl).toContain("pub fn toggle_maximize()");
    expect(rustWindowControl).toContain("pub fn minimize_foreground()");
  });

  it("前端事件名必须与 Rust 常量逐字一致，否则订阅永远收不到", () => {
    expect(EVENTS.foregroundChanged).toBe(rustEventName("EVENT_FOREGROUND_CHANGED"));
    expect(EVENTS.statusTick).toBe(rustEventName("EVENT_STATUS_TICK"));
    expect(EVENTS.resourceTick).toBe(rustEventName("EVENT_RESOURCE_TICK"));
    expect(EVENTS.wifiChanged).toBe(rustEventName("EVENT_WIFI_CHANGED"));
    expect(EVENTS.volumeChanged).toBe(rustEventName("EVENT_VOLUME_CHANGED"));
    expect(EVENTS.batteryChanged).toBe(rustEventName("EVENT_BATTERY_CHANGED"));
    expect(EVENTS.popupShow).toBe(rustEventName("EVENT_POPUP_SHOW"));
  });
});
