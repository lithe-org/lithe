import { act, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import App from "./App";
import { initialSystemSnapshot, useSystemStore } from "./stores/system";
import { initialUiSnapshot, useUiStore } from "./stores/ui";

// store 是模块级单例，用例间必须复位，否则前一个用例的状态会泄漏到下一个。
beforeEach(() => {
  useSystemStore.setState(initialSystemSnapshot);
  useUiStore.setState(initialUiSnapshot);
});

describe("顶栏骨架", () => {
  it("清理模板后只渲染占位文本，不再出现示例文案与图标", () => {
    const { container } = render(<App />);

    expect(container.querySelector('[data-testid="lithe-root"]')).not.toBeNull();
    expect(screen.queryByText(/Welcome to Tauri/)).toBeNull();
    expect(container.querySelector("img")).toBeNull();
    expect(container.querySelector("form")).toBeNull();
  });

  it("store setter 变更后顶栏占位文本即时刷新，验证订阅链路可用", () => {
    render(<App />);
    expect(screen.getByTestId("foreground-app").textContent).toBe("应用");

    act(() => {
      useSystemStore.getState().setForegroundApp("记事本");
      useUiStore.getState().setLanguage("en-US");
    });

    expect(screen.getByTestId("foreground-app").textContent).toBe("记事本");
    expect(screen.getByTestId("language").textContent).toBe("en-US");
  });
});
