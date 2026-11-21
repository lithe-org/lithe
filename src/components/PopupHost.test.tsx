import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PopupHost } from "./PopupHost";

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => cleanup());

describe("下拉窗口宿主", () => {
  it("渲染铺满窗口的空占位容器，等待后续注入菜单内容", () => {
    render(<PopupHost />);
    const host = screen.getByTestId("popup-host");

    expect(host.className).toContain("h-full");
    expect(host.className).toContain("w-full");
    expect(host.className).toContain("bg-[var(--lithe-panel-tint)]");
    expect(host.className).not.toContain("bar-tint");
    expect(host.childElementCount).toBe(0);
  });

  it("收到 popup:show 注入后按 kind 渲染 Logo 菜单", async () => {
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (_event, callback) => {
      handler = callback as unknown as (event: unknown) => void;
      return () => {};
    });

    await act(async () => {
      render(<PopupHost />);
    });

    await act(async () => {
      handler?.({ payload: { kind: "logo" } });
    });

    expect(screen.getByTestId("logo-menu")).not.toBeNull();
  });

  it("收到 control-center 注入后渲染控制中心面板", async () => {
    let handler: ((event: unknown) => void) | undefined;
    vi.mocked(listen).mockImplementation(async (_event, callback) => {
      handler = callback as unknown as (event: unknown) => void;
      return () => {};
    });

    await act(async () => {
      render(<PopupHost />);
    });
    await act(async () => {
      handler?.({ payload: { kind: "control-center", focus: "network" } });
    });

    expect(screen.getByTestId("control-center")).not.toBeNull();
    expect(screen.getByTestId("control-module-network")).not.toBeNull();
  });

  it("注入事件丢失时靠主动拉取补上面板内容", async () => {
    vi.mocked(invoke).mockImplementation(async (command) => {
      if (command === "get_popup_state") {
        return { kind: "logo" };
      }
      return undefined;
    });

    await act(async () => {
      render(<PopupHost />);
    });
    await act(async () => {
      await Promise.resolve();
    });

    expect(invoke).toHaveBeenCalledWith("get_popup_state");
    expect(screen.getByTestId("logo-menu")).not.toBeNull();
  });

  it("按 Esc 调用 hide_popup 收起面板", () => {
    render(<PopupHost />);
    fireEvent.keyDown(window, { key: "Escape" });

    expect(invoke).toHaveBeenCalledWith("hide_popup");
  });
});
