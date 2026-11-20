import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { SettingsWindow } from "./SettingsWindow";

afterEach(() => cleanup());

describe("设置窗口", () => {
  it("左侧六个分类，右侧默认显示通用页", () => {
    render(<SettingsWindow />);

    const expected = [
      "general",
      "appearance",
      "statusBar",
      "behavior",
      "updates",
      "about",
    ];
    const rendered = Array.from(
      screen.getByRole("navigation").querySelectorAll("button"),
    ).map((button) => button.getAttribute("data-testid"));
    expect(rendered).toEqual(expected.map((id) => `settings-nav-${id}`));
    expect(screen.getByTestId("general-page")).not.toBeNull();
    expect(
      screen.getByTestId("settings-nav-general").getAttribute("aria-current"),
    ).toBe("page");
  });

  it("切换分类后右侧内容随之切换", () => {
    render(<SettingsWindow />);

    fireEvent.click(screen.getByTestId("settings-nav-about"));

    expect(screen.getByTestId("about-page")).not.toBeNull();
    expect(screen.queryByTestId("general-page")).toBeNull();
    expect(
      screen.getByTestId("settings-nav-about").getAttribute("aria-current"),
    ).toBe("page");
  });
});
