import { invoke } from "@tauri-apps/api/core";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../../stores/system";
import { ClockDisplay } from "./ClockDisplay";
import { NotificationIcon } from "./NotificationIcon";

beforeEach(() => {
  vi.clearAllMocks();
  useSystemStore.setState(initialSystemSnapshot);
});

afterEach(() => cleanup());

async function clickAndWait(element: HTMLElement) {
  await act(async () => {
    fireEvent.click(element);
    await Promise.resolve();
  });
}

describe("通知与时间入口", () => {
  it("点击通知图标先收起面板再发送 Win+N", async () => {
    render(<NotificationIcon />);
    await clickAndWait(screen.getByTestId("status-notification"));

    const commands = vi.mocked(invoke).mock.calls.map(([name]) => name);
    expect(commands).toEqual(["close_popup", "open_notification_center"]);
  });

  it("点击时间同样先收面板再走原生入口", async () => {
    render(<ClockDisplay time="09:05" />);
    await clickAndWait(screen.getByTestId("clock"));

    const commands = vi.mocked(invoke).mock.calls.map(([name]) => name);
    expect(commands).toEqual(["close_popup", "open_notification_center"]);
  });

  it("没有面板可收时仍然发送快捷键", async () => {
    vi.mocked(invoke).mockImplementation((async (command: string) => {
      if (command === "close_popup") {
        throw new Error("no popup");
      }
      return undefined;
    }) as never);

    render(<NotificationIcon />);
    await clickAndWait(screen.getByTestId("status-notification"));

    expect(invoke).toHaveBeenCalledWith("open_notification_center");
  });
});
