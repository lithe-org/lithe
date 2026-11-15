import { invoke } from "@tauri-apps/api/core";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SplitMenu } from "./SplitMenu";

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => cleanup());

describe("分屏布局面板", () => {
  it("渲染六种布局，每种都有独立示意图标", () => {
    render(<SplitMenu />);

    expect(screen.getAllByRole("button")).toHaveLength(6);
    for (const label of ["左半", "右半", "左上", "右上", "左下", "右下"]) {
      expect(screen.getByRole("button", { name: label })).not.toBeNull();
    }
  });

  it("示意图标高亮的格子数与布局覆盖区域一致", () => {
    render(<SplitMenu />);

    const highlighted = (id: string) =>
      Array.from(
        screen.getByTestId(`split-layout-${id}`).querySelectorAll("[data-active]"),
      ).filter((cell) => cell.getAttribute("data-active") === "true").length;

    // 左右半屏各占两格，四象限各占一格。
    expect(highlighted("left")).toBe(2);
    expect(highlighted("right")).toBe(2);
    expect(highlighted("top-left")).toBe(1);
    expect(highlighted("bottom-right")).toBe(1);
  });

  it("点击布局先收起面板再下发分屏命令", async () => {
    render(<SplitMenu />);

    fireEvent.click(screen.getByTestId("split-layout-top-left"));
    expect(invoke).toHaveBeenCalledWith("hide_popup");

    await Promise.resolve();
    expect(invoke).toHaveBeenCalledWith("apply_split", { layout: "top-left" });
  });
});
