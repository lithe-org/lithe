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
    expect(root.style.getPropertyValue("--lithe-accent")).toBe("#AABBCC");
  });

  it("模式选择持久化，重启后仍生效", () => {
    useThemeStore.getState().setMode("light");
    expect(window.localStorage.getItem("lithe.theme-mode")).toBe("light");
  });
});
