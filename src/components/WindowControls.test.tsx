import { invoke } from "@tauri-apps/api/core";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { WindowControls } from "./WindowControls";

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(invoke).mockResolvedValue({
    name: "记事本",
    executable: "notepad.exe",
    controllable: true,
  });
});

afterEach(() => cleanup());

describe("中段窗口控制按钮组", () => {
  it("按 PRD 5.3.1 渲染置顶、最大化、最小化、分屏四个按钮", () => {
    render(<WindowControls />);

    expect(screen.getAllByRole("button")).toHaveLength(4);
    for (const label of ["置顶", "最大化", "最小化", "分屏"]) {
      expect(screen.getByRole("button", { name: label })).not.toBeNull();
    }
  });

  it("悬停时拉取前台应用名，作为每个按钮的操作目标提示", async () => {
    render(<WindowControls />);

    await act(async () => {
      fireEvent.mouseEnter(screen.getByTestId("window-controls"));
    });

    expect(invoke).toHaveBeenCalledWith("get_foreground_app");
    expect(
      screen.getByTestId("window-control-pin").getAttribute("title"),
    ).toBe("置顶：记事本");
  });

  it("拉取失败时退回只显示动作名，不留下空提示", async () => {
    vi.mocked(invoke).mockRejectedValue(new Error("ipc down"));
    render(<WindowControls />);

    await act(async () => {
      fireEvent.mouseEnter(screen.getByTestId("window-controls"));
    });

    expect(
      screen.getByTestId("window-control-split").getAttribute("title"),
    ).toBe("分屏");
  });
});
