import { listen } from "@tauri-apps/api/event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../stores/system";
import { EVENTS, registerSystemEvents } from "./events";

beforeEach(() => {
  vi.clearAllMocks();
  useSystemStore.setState(initialSystemSnapshot);
});

describe("前台切换事件订阅", () => {
  it("订阅 Rust 同名事件并把应用名写入 store，卸载时可解绑", async () => {
    const unlisten = vi.fn();
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (_event, callback) => {
      handler = callback as unknown as (event: unknown) => void;
      return unlisten;
    });

    const unlisteners = await registerSystemEvents();

    expect(listen).toHaveBeenCalledWith(EVENTS.foregroundChanged, expect.any(Function));
    expect(unlisteners).toHaveLength(1);

    handler?.({
      payload: { name: "记事本", executable: "notepad.exe", controllable: true },
    });
    expect(useSystemStore.getState().foregroundApp).toBe("记事本");

    unlisteners.forEach((fn) => fn());
    expect(unlisten).toHaveBeenCalledTimes(1);
  });
});
