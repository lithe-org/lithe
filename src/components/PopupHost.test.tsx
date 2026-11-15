import { invoke } from "@tauri-apps/api/core";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
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
    expect(host.childElementCount).toBe(0);
  });

  it("按 Esc 调用 hide_popup 收起面板", () => {
    render(<PopupHost />);
    fireEvent.keyDown(window, { key: "Escape" });

    expect(invoke).toHaveBeenCalledWith("hide_popup");
  });
});
