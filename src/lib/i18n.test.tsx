import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { LogoMenu } from "../components/menus/LogoMenu";
import i18n, { LANGUAGE_KEY, setLanguage } from "./i18n";

afterEach(() => cleanup());

describe("国际化", () => {
  it("默认使用中文且补齐缺失键的回退", () => {
    expect(i18n.t("menu.systemInfo")).toBe("系统信息");
    expect(i18n.t("status.cpuMemory", { cpu: 13, memory: 35 })).toBe(
      "CPU 13%，内存 35%",
    );
  });

  it("切换英文后关键文案与插值格式变化", async () => {
    await i18n.changeLanguage("en-US");

    expect(i18n.t("menu.systemInfo")).toBe("System information");
    expect(i18n.t("status.cpuMemory", { cpu: 13, memory: 35 })).toBe(
      "CPU 13%, memory 35%",
    );
    expect(i18n.t("volumePanel.unmute")).toBe("Unmute");
  });

  it("组件跟随语言切换，选择被持久化", async () => {
    render(<LogoMenu />);
    expect(screen.getByText("系统信息")).not.toBeNull();

    await act(async () => {
      setLanguage("en-US");
      await i18n.changeLanguage("en-US");
    });

    expect(screen.getByText("System information")).not.toBeNull();
    expect(window.localStorage.getItem(LANGUAGE_KEY)).toBe("en-US");
  });
});
