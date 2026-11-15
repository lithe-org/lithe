import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { PopupHost } from "./PopupHost";

afterEach(() => cleanup());

describe("下拉窗口宿主", () => {
  it("渲染铺满窗口的空占位容器，等待后续注入菜单内容", () => {
    render(<PopupHost />);
    const host = screen.getByTestId("popup-host");

    expect(host.className).toContain("h-full");
    expect(host.className).toContain("w-full");
    expect(host.childElementCount).toBe(0);
  });
});
