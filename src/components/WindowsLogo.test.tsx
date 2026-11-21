import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { WindowsLogo } from "./WindowsLogo";

afterEach(() => cleanup());

describe("Windows 11 logo", () => {
  it("使用用户提供的 windows-11.svg 资源", () => {
    render(<WindowsLogo />);

    const logo = document.querySelector(
      '[data-testid="windows-logo"]',
    ) as HTMLImageElement;
    expect(logo).not.toBeNull();
    expect(logo.tagName).toBe("IMG");
    // Vite 会把小 SVG 内联成 data URL，解码后仍是用户提供的那份图标。
    expect(logo.getAttribute("src")).toContain("data:image/svg+xml");
    expect(decodeURIComponent(logo.getAttribute("src") ?? "")).toContain(
      "M0 14v14h28V0H0z",
    );
  });

  it("尺寸走设计 token，与状态图标的 16px 基线保持一致", () => {
    render(<WindowsLogo />);

    const className =
      document.querySelector('[data-testid="windows-logo"]')?.getAttribute("class") ?? "";
    expect(className).toContain("size-[var(--lithe-icon-size)]");
  });
});
