import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";
import { initialSystemSnapshot, useSystemStore } from "./stores/system";
import { initialUiSnapshot, useUiStore } from "./stores/ui";
import { useSettingsStore } from "./stores/settings";

const defaultForegroundApp = {
  name: "应用",
  executable: "lithe.exe",
  controllable: true,
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(invoke).mockResolvedValue(defaultForegroundApp);
  vi.mocked(getCurrentWindow).mockReturnValue({ label: "main" } as never);
  // store 是模块级单例，用例间必须复位，否则前一个用例的状态会泄漏到下一个。
  useSystemStore.setState(initialSystemSnapshot);
  useUiStore.setState(initialUiSnapshot);
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

async function renderApp() {
  // 等 useEffect 里的 invoke Promise 落地，避免异步 setState 逃出 act。
  let view!: ReturnType<typeof render>;
  await act(async () => {
    view = render(<App />);
  });
  return view;
}

describe("顶栏骨架", () => {
  it("清理模板后只渲染顶栏，不再出现示例文案与图标", async () => {
    const { container } = await renderApp();

    expect(container.querySelector('[data-testid="lithe-root"]')).not.toBeNull();
    expect(screen.queryByText(/Welcome to Tauri/)).toBeNull();
    expect(container.querySelector("img")).toBeNull();
    expect(container.querySelector("form")).toBeNull();
  });

  it("渲染左中右三区，左区承载 logo 与应用名，中区留空，右区显示时间", async () => {
    await renderApp();

    expect(screen.getByTestId("zone-left")).not.toBeNull();
    expect(screen.getByTestId("zone-center")).not.toBeNull();
    expect(screen.getByTestId("zone-right")).not.toBeNull();

    expect(screen.getByTestId("windows-logo")).not.toBeNull();
    expect(screen.getByTestId("foreground-app").textContent).toBe("应用");
    expect(
      screen
        .getByTestId("zone-center")
        .querySelector('[data-testid="window-controls"]'),
    ).not.toBeNull();
    expect(screen.getByTestId("clock").textContent).toMatch(
      /^\d{4}\/\d{2}\/\d{2} \d{2}:\d{2}$/,
    );
  });

  it("store setter 变更后左侧应用名即时刷新，验证订阅链路可用", async () => {
    await renderApp();
    expect(screen.getByTestId("foreground-app").textContent).toBe("应用");

    act(() => {
      useSystemStore.getState().setForegroundApp("记事本");
    });

    expect(screen.getByTestId("foreground-app").textContent).toBe("记事本");
  });

  it("右侧时间读取 store 并在变更时刷新", async () => {
    await renderApp();

    act(() => {
      useSystemStore.getState().setTime("09:05");
    });

    expect(screen.getByTestId("clock").textContent).toBe("09:05");
  });

  it("启动时拉取一次前台应用契约并写入 store，验证前后端链路", async () => {
    vi.mocked(invoke).mockResolvedValue({
      name: "Visual Studio Code",
      executable: "Code.exe",
      controllable: true,
    });

    await renderApp();

    expect(invoke).toHaveBeenCalledWith("get_foreground_app");
    // 主题初始化也会发一次 IPC，这里只约束前台应用链路只拉一次。
    expect(
      vi.mocked(invoke).mock.calls.filter(([name]) => name === "get_foreground_app"),
    ).toHaveLength(1);
    expect(screen.getByTestId("foreground-app").textContent).toBe("Visual Studio Code");
  });

  it("关闭日期时间后顶栏不再渲染时钟，通知仍保留", async () => {
    useSettingsStore.setState({
      visibility: {
        ...useSettingsStore.getState().visibility,
        datetime: false,
      },
    });

    await renderApp();

    expect(screen.queryByTestId("clock")).toBeNull();
    expect(screen.getByTestId("status-notification")).not.toBeNull();
  });

  it("启用空白双击后，双击顶栏空白处最大化前台窗口", async () => {
    useSettingsStore.setState({ doubleClickOnEmpty: true });

    await renderApp();
    await act(async () => {
      fireEvent.doubleClick(screen.getByTestId("lithe-root"));
    });

    expect(invoke).toHaveBeenCalledWith("toggle_maximize");
  });

  it("点击 logo 调用 show_popup 并传入锚点矩形，由 Rust 负责落点", async () => {
    await renderApp();

    await act(async () => {
      fireEvent.click(screen.getByTestId("windows-logo-button"));
    });

    expect(invoke).toHaveBeenCalledWith("show_popup", {
      anchor: { x: 0, y: 0, width: 0, height: 0 },
      kind: "logo",
      focus: null,
    });
  });

  it("前台窗口不可控时隐藏中段按钮组，切回应用后恢复", async () => {
    vi.mocked(invoke).mockResolvedValue({
      ...defaultForegroundApp,
      controllable: false,
    });

    await renderApp();
    expect(screen.queryByTestId("window-controls")).toBeNull();

    act(() => {
      useSystemStore.getState().setForegroundControllable(true);
    });
    expect(screen.getByTestId("window-controls")).not.toBeNull();
  });

  it("popup label 的窗口渲染下拉宿主而不是顶栏，避免两套 UI 串台", async () => {
    vi.mocked(getCurrentWindow).mockReturnValue({ label: "popup-0" } as never);

    const { container } = await renderApp();

    expect(screen.getByTestId("popup-host")).not.toBeNull();
    expect(container.querySelector('[data-testid="lithe-root"]')).toBeNull();
  });

  it("重渲染不会重复订阅前台切换事件，避免解绑重绑丢事件", async () => {
    const view = await renderApp();
    const before = vi.mocked(listen).mock.calls.length;

    await act(async () => {
      view.rerender(<App />);
    });

    expect(vi.mocked(listen).mock.calls.length).toBe(before);
  });
});
