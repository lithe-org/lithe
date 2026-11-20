import { listen } from "@tauri-apps/api/event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../stores/system";
import { useThemeStore } from "../stores/theme";
import { EVENTS, registerSystemEvents } from "./events";

beforeEach(() => {
  vi.clearAllMocks();
  useSystemStore.setState(initialSystemSnapshot);
});

describe("系统事件订阅", () => {
  it("订阅 Rust 同名事件并把应用名与可控性写入 store，卸载时逐个解绑", async () => {
    const unlisteners = [vi.fn(), vi.fn(), vi.fn(), vi.fn(), vi.fn(), vi.fn()];
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (eventName, callback) => {
      if (eventName === EVENTS.foregroundChanged) {
        handler = callback as unknown as (event: unknown) => void;
      }
      return unlisteners.shift() ?? (() => {});
    });

    const registered = await registerSystemEvents();

    expect(listen).toHaveBeenCalledWith(
      EVENTS.foregroundChanged,
      expect.any(Function),
    );
    expect(listen).toHaveBeenCalledWith(EVENTS.resourceTick, expect.any(Function));
    expect(registered).toHaveLength(6);

    handler?.({
      payload: { name: "记事本", executable: "notepad.exe", controllable: true },
    });
    expect(useSystemStore.getState().foregroundApp).toBe("记事本");
    expect(useSystemStore.getState().foregroundControllable).toBe(true);

    registered.forEach((fn) => fn());
    expect(useSystemStore.getState().foregroundApp).toBe("记事本");
  });

  it("Wi-Fi 状态变化写入 store，供图标决定显隐与强度", async () => {
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (eventName, callback) => {
      if (eventName === EVENTS.wifiChanged) {
        handler = callback as unknown as (event: unknown) => void;
      }
      return () => {};
    });

    await registerSystemEvents();
    handler?.({
      payload: {
        available: true,
        connected: true,
        ssid: "Home",
        signal: 72,
      },
    });

    expect(useSystemStore.getState().wifi).toEqual({
      available: true,
      connected: true,
      ssid: "Home",
      signal: 72,
    });
  });

  it("音量变化写入 store，供图标与控制面板共用", async () => {
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (eventName, callback) => {
      if (eventName === EVENTS.volumeChanged) {
        handler = callback as unknown as (event: unknown) => void;
      }
      return () => {};
    });

    await registerSystemEvents();
    handler?.({ payload: { level: 42, muted: true } });

    expect(useSystemStore.getState().volume).toEqual({ level: 42, muted: true });
  });

  it("电池状态变化写入 store，台式机按无电池处理", async () => {
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (eventName, callback) => {
      if (eventName === EVENTS.batteryChanged) {
        handler = callback as unknown as (event: unknown) => void;
      }
      return () => {};
    });

    await registerSystemEvents();
    handler?.({
      payload: { present: true, percent: 88, charging: true },
    });
    expect(useSystemStore.getState().battery).toEqual({
      present: true,
      percent: 88,
      charging: true,
    });

    handler?.({ payload: { present: false, percent: 0, charging: false } });
    expect(useSystemStore.getState().battery.present).toBe(false);
  });

  it("系统主题变化写入 theme store，跟随系统档直接生效", async () => {
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (eventName, callback) => {
      if (eventName === EVENTS.themeChanged) {
        handler = callback as unknown as (event: unknown) => void;
      }
      return () => {};
    });

    await registerSystemEvents();
    handler?.({ payload: { dark: true, accent: "#AABBCC" } });

    expect(useThemeStore.getState().dark).toBe(true);
    expect(useThemeStore.getState().accent).toBe("#AABBCC");
  });

  it("资源 tick 把 CPU 与内存百分比写入 store", async () => {
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (eventName, callback) => {
      if (eventName === EVENTS.resourceTick) {
        handler = callback as unknown as (event: unknown) => void;
      }
      return () => {};
    });

    await registerSystemEvents();
    handler?.({ payload: { cpuPercent: 12.5, memoryPercent: 34 } });

    expect(useSystemStore.getState().cpuPercent).toBe(12.5);
    expect(useSystemStore.getState().memoryPercent).toBe(34);
  });
});
