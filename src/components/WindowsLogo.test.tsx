import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { WindowsLogo } from "./WindowsLogo";

afterEach(() => cleanup());

describe("Windows 11 logo", () => {
  it("用四格 SVG 绘制，颜色取 currentColor 跟随主题前景色", () => {
    const { container } = render(<WindowsLogo />);
    const svg = container.querySelector("svg");

    expect(svg).not.toBeNull();
    expect(svg?.getAttribute("data-testid")).toBe("windows-logo");

    const panes = Array.from(svg?.querySelectorAll("rect") ?? []);
    expect(panes).toHaveLength(4);
    // currentColor 让亮/暗主题只切换 CSS 变量，不用维护两套图标。
    expect(panes.every((pane) => pane.getAttribute("fill") === "currentColor")).toBe(true);
  });

  it("尺寸走设计 token，与状态图标的 16px 基线保持一致", () => {
    const { container } = render(<WindowsLogo />);
    const className = container.querySelector("svg")?.getAttribute("class") ?? "";

    expect(className).toContain("size-[var(--lithe-icon-size)]");
  });
});
