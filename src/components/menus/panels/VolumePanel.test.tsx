import { invoke } from "@tauri-apps/api/core";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../../../stores/system";
import { VolumePanel } from "./VolumePanel";

beforeEach(() => {
  vi.clearAllMocks();
  useSystemStore.setState({
    ...initialSystemSnapshot,
    volume: { level: 42, muted: false },
  });
});

afterEach(() => cleanup());

describe("控制中心音量模块", () => {
  it("显示当前音量并可拖动滑块下发新值", async () => {
    render(<VolumePanel />);
    const slider = screen.getByTestId("volume-slider") as HTMLInputElement;
    expect(slider.value).toBe("42");

    await act(async () => {
      fireEvent.change(slider, { target: { value: "70" } });
    });

    expect(invoke).toHaveBeenCalledWith("set_volume", { level: 70 });
    expect(screen.getByText("70%")).not.toBeNull();
  });

  it("静音按钮下发 toggle_mute，静音后文案变为取消静音", async () => {
    render(<VolumePanel />);
    const button = screen.getByTestId("volume-mute");
    expect(button.textContent).toBe("静音");

    await act(async () => {
      fireEvent.click(button);
    });
    expect(invoke).toHaveBeenCalledWith("toggle_mute");

    act(() => {
      useSystemStore.getState().patch({ volume: { level: 42, muted: true } });
    });
    expect(screen.getByTestId("volume-mute").textContent).toBe("取消静音");
  });

  it("系统事件回来前保留草稿值，不被旧音量回弹", async () => {
    render(<VolumePanel />);
    const slider = screen.getByTestId("volume-slider") as HTMLInputElement;

    await act(async () => {
      fireEvent.change(slider, { target: { value: "88" } });
    });

    // store 仍是 42，但滑块与百分比必须显示草稿 88。
    expect(slider.value).toBe("88");
    expect(screen.getByText("88%")).not.toBeNull();
  });
});
