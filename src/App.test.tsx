import { invoke } from "@tauri-apps/api/core";
import { act, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";
import { initialSystemSnapshot, useSystemStore } from "./stores/system";
import { initialUiSnapshot, useUiStore } from "./stores/ui";

const defaultForegroundApp = {
  name: "应用",
  executable: "lithe.exe",
  controllable: true,
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(invoke).mockResolvedValue(defaultForegroundApp);
  // store 是模块级单例，用例间必须复位，否则前一个用例的状态会泄漏到下一个。
  useSystemStore.setState(initialSystemSnapshot);
  useUiStore.setState(initialUiSnapshot);
});

async function renderApp() {
  // 等 useEffect 里的 invoke Promise 落地，避免异步 setState 逃出 act。
  let view!: ReturnType<typeof render>;
  await act(async () => {
    view = render(<App />);
  });
  return view;
}

describe("顶栏骨架", () => {
  it("清理模板后只渲染占位文本，不再出现示例文案与图标", async () => {
    const { container } = await renderApp();

    expect(container.querySelector('[data-testid="lithe-root"]')).not.toBeNull();
    expect(screen.queryByText(/Welcome to Tauri/)).toBeNull();
    expect(container.querySelector("img")).toBeNull();
    expect(container.querySelector("form")).toBeNull();
  });

  it("store setter 变更后顶栏占位文本即时刷新，验证订阅链路可用", async () => {
    await renderApp();
    expect(screen.getByTestId("foreground-app").textContent).toBe("应用");

    act(() => {
      useSystemStore.getState().setForegroundApp("记事本");
      useUiStore.getState().setLanguage("en-US");
    });

    expect(screen.getByTestId("foreground-app").textContent).toBe("记事本");
    expect(screen.getByTestId("language").textContent).toBe("en-US");
  });

  it("启动时拉取一次前台应用契约并写入 store，验证前后端链路", async () => {
    vi.mocked(invoke).mockResolvedValue({
      name: "Visual Studio Code",
      executable: "Code.exe",
      controllable: true,
    });

    await renderApp();

    expect(invoke).toHaveBeenCalledWith("get_foreground_app");
    expect(invoke).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("foreground-app").textContent).toBe("Visual Studio Code");
  });
});
