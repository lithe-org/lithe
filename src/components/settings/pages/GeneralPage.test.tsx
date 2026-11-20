import { disable, enable, isEnabled } from "@tauri-apps/plugin-autostart";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import i18n, { LANGUAGE_KEY } from "../../../lib/i18n";
import { GeneralPage } from "./GeneralPage";

afterEach(() => cleanup());

describe("通用设置", () => {
  it("开机自启开关调用插件，检查更新仍占位禁用", async () => {
    render(<GeneralPage />);

    // 先等 isEnabled() 的初始查询落地，避免它随后覆盖点击设置的状态。
    await act(async () => {
      await Promise.resolve();
    });

    const startup = screen.getByTestId("startup-toggle") as HTMLInputElement;
    expect(startup.disabled).toBe(false);
    expect(isEnabled).toHaveBeenCalled();

    await act(async () => {
      fireEvent.click(startup);
    });
    expect(enable).toHaveBeenCalled();
    expect(startup.checked).toBe(true);
    expect(disable).not.toHaveBeenCalled();

    expect(
      (screen.getByTestId("check-updates") as HTMLButtonElement).disabled,
    ).toBe(true);
  });

  it("切换英文立即生效并持久化", async () => {
    render(<GeneralPage />);
    expect(screen.getByText("界面语言")).not.toBeNull();

    await act(async () => {
      fireEvent.click(screen.getByTestId("language-en-US"));
    });

    expect(i18n.language).toBe("en-US");
    expect(screen.getByText("Interface language")).not.toBeNull();
    expect(window.localStorage.getItem(LANGUAGE_KEY)).toBe("en-US");
    expect(
      screen.getByTestId("language-en-US").getAttribute("aria-pressed"),
    ).toBe("true");
  });
});
