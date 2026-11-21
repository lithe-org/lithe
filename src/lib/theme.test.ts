import { beforeEach, describe, expect, it } from "vitest";
import { resolveDark, useThemeStore } from "../stores/theme";
import { applyTheme } from "./theme";

beforeEach(() => {
  window.localStorage.clear();
  useThemeStore.setState({ mode: "system", dark: false, accent: "#0078D4" });
  document.documentElement.classList.remove("dark");
});

describe("主题系统", () => {
  it("三档模式解析明暗正确", () => {
    expect(resolveDark({ mode: "light", dark: true })).toBe(false);
    expect(resolveDark({ mode: "dark", dark: false })).toBe(true);
    expect(resolveDark({ mode: "system", dark: true })).toBe(true);
    expect(resolveDark({ mode: "system", dark: false })).toBe(false);
  });

  it("applyTheme 同步 dark 类、着色层与强调色变量", () => {
    useThemeStore.setState({ mode: "dark", dark: false, accent: "#AABBCC" });

    applyTheme();

    const root = document.documentElement;
    expect(root.classList.contains("dark")).toBe(true);
    expect(root.style.getPropertyValue("--lithe-bar-tint")).toBe(
      "rgba(18, 18, 20, 0.62)",
    );
    expect(root.style.getPropertyValue("--lithe-panel-tint")).toBe(
      "rgba(18, 18, 20, 0.8)",
    );
    expect(root.style.getPropertyValue("--lithe-accent")).toBe("#AABBCC");
  });

  it("浅色面板着色比顶栏更实，保证菜单文字可读", () => {
    applyTheme();

    const root = document.documentElement;
    expect(root.style.getPropertyValue("--lithe-bar-tint")).toBe(
      "rgba(255, 255, 255, 0.32)",
    );
    expect(root.style.getPropertyValue("--lithe-panel-tint")).toBe(
      "rgba(255, 255, 255, 0.8)",
    );
  });

  it("自定义强调色不会被系统主题变化覆盖", () => {
    useThemeStore.getState().setAccent("#112233");
    useThemeStore.getState().applySystemTheme({
      dark: true,
      accent: "#AABBCC",
    });

    expect(useThemeStore.getState().accent).toBe("#112233");
    expect(useThemeStore.getState().dark).toBe(true);
  });

  it("模式选择持久化，重启后仍生效", () => {
    useThemeStore.getState().setMode("light");
    expect(window.localStorage.getItem("lithe.theme-mode")).toBe("light");
  });
});
