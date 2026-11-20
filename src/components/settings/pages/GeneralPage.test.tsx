import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import i18n, { LANGUAGE_KEY } from "../../../lib/i18n";
import { GeneralPage } from "./GeneralPage";

afterEach(() => cleanup());

describe("通用设置", () => {
  it("开机自启与检查更新先占位禁用", () => {
    render(<GeneralPage />);

    expect(
      (screen.getByTestId("startup-toggle") as HTMLInputElement).disabled,
    ).toBe(true);
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
