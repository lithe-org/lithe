import { invoke } from "@tauri-apps/api/core";
import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../../../stores/system";
import { NetworkPanel } from "./NetworkPanel";

beforeEach(() => {
  vi.clearAllMocks();
  useSystemStore.setState({
    ...initialSystemSnapshot,
    wifi: { available: true, connected: true, ssid: "Home", signal: 80 },
  });
});

afterEach(() => cleanup());

async function renderPanel(networks: unknown) {
  vi.mocked(invoke).mockResolvedValue(networks);
  await act(async () => {
    render(<NetworkPanel />);
  });
}

describe("控制中心网络模块", () => {
  it("展示当前连接与可用网络列表（含信号与加密标记）", async () => {
    await renderPanel([
      { ssid: "Home", signal: 80, connected: true, secure: true },
      { ssid: "Cafe", signal: 40, connected: false, secure: false },
    ]);

    expect(screen.getByTestId("network-summary").textContent).toContain(
      "已连接 Home",
    );

    const home = screen.getByTestId("wifi-network-Home");
    expect(home.textContent).toContain("80%");
    expect(home.textContent).toContain("已连接");
    expect(home.querySelector('[aria-label="加密网络"]')).not.toBeNull();

    const cafe = screen.getByTestId("wifi-network-Cafe");
    expect(cafe.textContent).toContain("40%");
    expect(cafe.querySelector('[aria-label="加密网络"]')).toBeNull();
  });

  it("无适配器时提示无 Wi-Fi 且不渲染列表项", async () => {
    useSystemStore.setState({
      ...initialSystemSnapshot,
      wifi: { available: false, connected: false, ssid: "", signal: 0 },
    });
    await renderPanel([]);

    expect(screen.getByTestId("network-summary").textContent).toBe(
      "无 Wi-Fi 适配器",
    );
    expect(screen.getByText("未发现可用网络")).not.toBeNull();
  });

  it("拉取失败时给出可读提示而不是空白", async () => {
    vi.mocked(invoke).mockRejectedValue(new Error("wlan down"));
    await act(async () => {
      render(<NetworkPanel />);
    });

    expect(screen.getByText("无法获取网络列表")).not.toBeNull();
  });
});
