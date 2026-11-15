import { invoke } from "@tauri-apps/api/core";
import { readFileSync } from "node:fs";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { getForegroundApp, getSystemStatus } from "./api";
import { EVENTS } from "./events";

const rustEvents = readFileSync("src-tauri/src/commands/mod.rs", "utf8");
const rustCommands = readFileSync("src-tauri/src/commands/system.rs", "utf8");

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

  it("前端命令名必须能在 Rust 侧找到同名 command 函数", () => {
    expect(rustCommands).toContain("pub fn get_foreground_app()");
    expect(rustCommands).toContain("pub fn get_system_status()");
  });

  it("前端事件名必须与 Rust 常量逐字一致，否则订阅永远收不到", () => {
    expect(EVENTS.foregroundChanged).toBe(rustEventName("EVENT_FOREGROUND_CHANGED"));
    expect(EVENTS.statusTick).toBe(rustEventName("EVENT_STATUS_TICK"));
    expect(EVENTS.resourceTick).toBe(rustEventName("EVENT_RESOURCE_TICK"));
    expect(EVENTS.wifiChanged).toBe(rustEventName("EVENT_WIFI_CHANGED"));
    expect(EVENTS.volumeChanged).toBe(rustEventName("EVENT_VOLUME_CHANGED"));
    expect(EVENTS.batteryChanged).toBe(rustEventName("EVENT_BATTERY_CHANGED"));
  });
});
