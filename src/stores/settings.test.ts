import { beforeEach, describe, expect, it } from "vitest";
import { clampBarHeight, useSettingsStore } from "./settings";

beforeEach(() => {
  window.localStorage.clear();
  useSettingsStore.setState({
    visibility: {
      resource: true,
      wifi: true,
      volume: true,
      battery: true,
      datetime: true,
    },
    barHeight: 30,
    autoHideFullscreen: true,
    doubleClickOnEmpty: false,
  });
});

describe("设置持久化", () => {
  it("状态栏高度限制在 28~36 逻辑像素", () => {
    expect(clampBarHeight(10)).toBe(28);
    expect(clampBarHeight(34)).toBe(34);
    expect(clampBarHeight(100)).toBe(36);
    expect(clampBarHeight(Number.NaN)).toBe(30);
  });

  it("开关与高度写入 localStorage，重启后可恢复", () => {
    useSettingsStore.getState().toggleVisibility("wifi");
    useSettingsStore.getState().setBarHeight(33);

    const saved = JSON.parse(window.localStorage.getItem("lithe.settings")!);
    expect(saved.visibility.wifi).toBe(false);
    expect(saved.barHeight).toBe(33);
  });
});
