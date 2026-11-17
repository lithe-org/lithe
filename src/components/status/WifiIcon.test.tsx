import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../../stores/system";
import { WifiIcon } from "./WifiIcon";

beforeEach(() => {
  useSystemStore.setState(initialSystemSnapshot);
});

afterEach(() => cleanup());

describe("Wi-Fi 状态图标", () => {
  it("无 Wi-Fi 适配器时完全隐藏图标", () => {
    const { container } = render(<WifiIcon />);

    expect(screen.queryByTestId("status-wifi")).toBeNull();
    expect(container.childElementCount).toBe(0);
  });

  it("已连接显示 wifi 图标，用透明度表达信号强度", () => {
    act(() => {
      useSystemStore.getState().patch({
        wifi: { available: true, connected: true, ssid: "Home", signal: 60 },
      });
    });
    render(<WifiIcon />);

    const icon = screen.getByTestId("status-wifi").querySelector("svg");
    expect(icon?.getAttribute("class")).toContain("lucide-wifi");
    expect(screen.getByTestId("status-wifi").parentElement?.style.opacity).toBe(
      "0.6",
    );
    expect(screen.getByTestId("status-wifi").getAttribute("title")).toContain(
      "Home",
    );
  });

  it("断开时切换为 wifi-off 图标，透明度恢复 1", () => {
    act(() => {
      useSystemStore.getState().patch({
        wifi: { available: true, connected: false, ssid: "", signal: 0 },
      });
    });
    render(<WifiIcon />);

    const icon = screen.getByTestId("status-wifi").querySelector("svg");
    expect(icon?.getAttribute("class")).toContain("lucide-wifi-off");
    expect(screen.getByTestId("status-wifi").parentElement?.style.opacity).toBe(
      "1",
    );
  });

  it("极弱信号保留最低可见度，不会淡到看不见", () => {
    act(() => {
      useSystemStore.getState().patch({
        wifi: { available: true, connected: true, ssid: "Home", signal: 3 },
      });
    });
    render(<WifiIcon />);

    expect(screen.getByTestId("status-wifi").parentElement?.style.opacity).toBe(
      "0.45",
    );
  });
});
