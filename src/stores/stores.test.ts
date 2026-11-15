import { beforeEach, describe, expect, it } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "./system";
import { initialUiSnapshot, useUiStore } from "./ui";

// zustand store 是模块级单例，逐例复位才能保证断言与用例顺序无关。
beforeEach(() => {
  useSystemStore.setState(initialSystemSnapshot);
  useUiStore.setState(initialUiSnapshot);
});

describe("system store 骨架", () => {
  it("前台应用名 setter 只改自身字段，不牵动已采集的资源数据", () => {
    useSystemStore.getState().setResourceUsage(12, 34);
    useSystemStore.getState().setForegroundApp("Google Chrome");

    const state = useSystemStore.getState();
    expect(state.foregroundApp).toBe("Google Chrome");
    expect(state.cpuPercent).toBe(12);
    expect(state.memoryPercent).toBe(34);
  });

  it("资源 setter 必须同时写入 CPU 与内存，避免顶栏只更新一半", () => {
    useSystemStore.getState().setResourceUsage(23, 61);

    const state = useSystemStore.getState();
    expect(state.cpuPercent).toBe(23);
    expect(state.memoryPercent).toBe(61);
  });

  it("patch 只覆盖给定字段，未涉及的嵌套对象保持同一引用", () => {
    const wifiBefore = useSystemStore.getState().wifi;
    useSystemStore.getState().patch({ time: "12:00" });

    // 全量重建对象会让下游 memo 恒不相等，这里钉住引用稳定性。
    expect(useSystemStore.getState().wifi).toBe(wifiBefore);
    expect(useSystemStore.getState().time).toBe("12:00");
  });
});

describe("ui store 骨架", () => {
  it("默认跟随系统主题、中文界面、无面板，符合 PRD 6.2 与第 7 章", () => {
    const state = useUiStore.getState();
    expect(state.theme).toBe("system");
    expect(state.language).toBe("zh-CN");
    expect(state.openPanel).toBeNull();
  });

  it("面板开关可在打开与关闭之间切换", () => {
    useUiStore.getState().setOpenPanel("control-center");
    expect(useUiStore.getState().openPanel).toBe("control-center");

    useUiStore.getState().setOpenPanel(null);
    expect(useUiStore.getState().openPanel).toBeNull();
  });
});
