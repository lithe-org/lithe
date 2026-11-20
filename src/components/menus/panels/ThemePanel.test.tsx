import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { useThemeStore } from "../../../stores/theme";
import { ThemePanel } from "./ThemePanel";

beforeEach(() => {
  window.localStorage.clear();
  useThemeStore.setState({
    mode: "system",
    dark: false,
    systemAccent: "#0078D4",
    accentOverride: null,
    accent: "#0078D4",
  });
});

afterEach(() => cleanup());

describe("控制中心主题模块", () => {
  it("三档模式切换写入 store 并持久化", () => {
    render(<ThemePanel />);

    fireEvent.click(screen.getByTestId("theme-mode-dark"));

    expect(useThemeStore.getState().mode).toBe("dark");
    expect(window.localStorage.getItem("lithe.theme-mode")).toBe("dark");
    expect(
      screen.getByTestId("theme-mode-dark").getAttribute("aria-pressed"),
    ).toBe("true");
  });

  it("点击预设色立即生效并记住选择", () => {
    render(<ThemePanel />);

    fireEvent.click(screen.getByTestId("accent-#D13438"));

    expect(useThemeStore.getState().accent).toBe("#D13438");
    expect(window.localStorage.getItem("lithe.accent")).toBe("#D13438");
    expect(screen.getByTestId("accent-current").textContent).toContain(
      "#D13438",
    );
  });

  it("自定义 hex 合法才生效，非法输入不改状态", () => {
    render(<ThemePanel />);
    const input = screen.getByTestId("accent-hex") as HTMLInputElement;

    fireEvent.change(input, { target: { value: "#ZZZZZZ" } });
    expect(useThemeStore.getState().accent).toBe("#0078D4");

    fireEvent.change(input, { target: { value: "#12AB34" } });
    expect(useThemeStore.getState().accent).toBe("#12AB34");
  });

  it("恢复按钮回到系统强调色", () => {
    act(() => {
      useThemeStore.getState().setAccent("#112233");
    });
    act(() => {
      useThemeStore.getState().applySystemTheme({
        dark: false,
        accent: "#AABBCC",
      });
    });
    render(<ThemePanel />);

    fireEvent.click(screen.getByTestId("accent-reset"));

    expect(useThemeStore.getState().accent).toBe("#AABBCC");
    expect(useThemeStore.getState().accentOverride).toBeNull();
    expect(window.localStorage.getItem("lithe.accent")).toBeNull();
  });
});
