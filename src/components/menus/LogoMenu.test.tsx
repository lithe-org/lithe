import { invoke } from "@tauri-apps/api/core";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LogoMenu } from "./LogoMenu";

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => cleanup());

describe("Logo 菜单", () => {
  it("按 PRD 5.2.1 顺序渲染 9 个菜单项与 3 条分隔线", () => {
    render(<LogoMenu />);

    const labels = screen
      .getAllByRole("menuitem")
      .map((item) => item.textContent);
    expect(labels).toEqual([
      "系统信息",
      "设置",
      "锁定",
      "睡眠",
      "重启",
      "关机",
      "任务管理器",
      "设置 Lithe",
      "关于 Lithe",
    ]);
    expect(screen.getAllByTestId("menu-separator")).toHaveLength(3);
  });

  it("点击系统信息先收起面板再调用对应命令", async () => {
    render(<LogoMenu />);

    fireEvent.click(screen.getByTestId("menu-item-system-info"));
    expect(invoke).toHaveBeenCalledWith("hide_popup");

    await Promise.resolve();
    expect(invoke).toHaveBeenCalledWith("open_system_info");
  });

  it("任务管理器走独立命令，不与系统信息混用", async () => {
    render(<LogoMenu />);

    fireEvent.click(screen.getByTestId("menu-item-task-manager"));
    await Promise.resolve();

    expect(invoke).toHaveBeenCalledWith("open_task_manager");
  });

  it("设置与关于 Lithe 在窗口实现前保持禁用，避免点了没反应", () => {
    render(<LogoMenu />);

    const settings = screen.getByTestId(
      "menu-item-lithe-settings",
    ) as HTMLButtonElement;
    const about = screen.getByTestId(
      "menu-item-lithe-about",
    ) as HTMLButtonElement;

    expect(settings.disabled).toBe(true);
    expect(about.disabled).toBe(true);

    fireEvent.click(settings);
    expect(invoke).not.toHaveBeenCalled();
  });
});
