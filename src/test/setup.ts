import { beforeEach, vi } from "vitest";

// Tauri API 只在桌面运行时存在，组件测试统一打桩，避免真实 IPC 让用例挂起。
vi.mock("@tauri-apps/api/core", () => ({
  invoke: vi.fn(async () => undefined),
}));

vi.mock("@tauri-apps/api/event", () => ({
  listen: vi.fn(async () => () => {}),
  emit: vi.fn(async () => {}),
}));

// jsdom 的 localStorage 在同一文件内跨用例共享，组件若落盘状态必须逐例清理。
beforeEach(() => {
  localStorage.clear();
});
