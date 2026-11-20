import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { useThemeStore } from "../../../stores/theme";
import { AppearancePage } from "./AppearancePage";

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

describe("外观设置", () => {
  it("主题三档切换写入 store", () => {
    render(<AppearancePage />);

    fireEvent.click(screen.getByTestId("appearance-mode-dark"));

    expect(useThemeStore.getState().mode).toBe("dark");
    expect(
      screen.getByTestId("appearance-mode-dark").getAttribute("aria-pressed"),
    ).toBe("true");
  });

  it("预设色与自定义 hex 都更新强调色", () => {
    render(<AppearancePage />);

    fireEvent.click(screen.getByTestId("appearance-accent-#D13438"));
    expect(useThemeStore.getState().accent).toBe("#D13438");

    fireEvent.change(screen.getByTestId("appearance-accent-hex"), {
      target: { value: "#12AB34" },
    });
    expect(useThemeStore.getState().accent).toBe("#12AB34");
  });
});
