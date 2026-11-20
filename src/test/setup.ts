import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach, vi } from "vitest";
import i18n from "../lib/i18n";

// Tauri API 只在桌面运行时存在，组件测试统一打桩，避免真实 IPC 让用例挂起。
vi.mock("@tauri-apps/api/core", () => ({
  invoke: vi.fn(async () => undefined),
}));

vi.mock("@tauri-apps/api/event", () => ({
  listen: vi.fn(async () => () => {}),
  emit: vi.fn(async () => {}),
}));

// 顶栏与下拉窗口共用 bundle，测试里默认按顶栏窗口渲染，个别用例可覆盖 label。
vi.mock("@tauri-apps/api/window", () => ({
  getCurrentWindow: vi.fn(() => ({ label: "main" })),
}));

vi.mock("@tauri-apps/api/webviewWindow", () => ({
  WebviewWindow: {
    getByLabel: vi.fn(async () => ({
      show: vi.fn(async () => {}),
      setFocus: vi.fn(async () => {}),
    })),
  },
}));

// jsdom 的 localStorage 在同一文件内跨用例共享，组件若落盘状态必须逐例清理。
// 未开启 globals 时 RTL 不会自动注册清理钩子，残留 DOM 会让 getByTestId 命中多个节点。
afterEach(() => cleanup());

beforeEach(() => {
  localStorage.clear();
  // 语言是模块级单例，用例间复位成默认中文，避免英文文案泄漏到别的用例。
  void i18n.changeLanguage("zh-CN");
});
