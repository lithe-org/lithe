import { invoke } from "@tauri-apps/api/core";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ControlCenter } from "./ControlCenter";

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => cleanup());

describe("控制中心骨架", () => {
  it("按 PRD 5.4.3 顺序渲染网络、音量、电池、资源、主题五模块", () => {
    const view = render(<ControlCenter />);
    const ids = Array.from(view.container.querySelectorAll("[data-module]")).map(
      (node) => node.getAttribute("data-module"),
    );

    expect(ids).toEqual(["network", "volume", "battery", "resource", "theme"]);
    expect(screen.getByTestId("control-center")).not.toBeNull();
  });

  it("挂载后按内容高度请求调整面板尺寸", () => {
    render(<ControlCenter />);

    // jsdom 的 scrollHeight 为 0，走到最小高度上限 120。
    expect(invoke).toHaveBeenCalledWith("resize_popup", { height: 120 });
  });

  it("带 focus 时滚动到对应模块", () => {
    const scrollIntoView = vi.fn();
    const view = render(<ControlCenter />);
    const target = view.container.querySelector('[data-module="resource"]');
    expect(target).not.toBeNull();
    Object.defineProperty(target!, "scrollIntoView", {
      value: scrollIntoView,
      configurable: true,
    });

    view.rerender(<ControlCenter focus="resource" />);

    expect(scrollIntoView).toHaveBeenCalledWith({ block: "start" });
  });
});
