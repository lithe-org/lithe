import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../../stores/system";
import { ResourceIcon } from "./ResourceIcon";

beforeEach(() => {
  useSystemStore.setState(initialSystemSnapshot);
});

afterEach(() => cleanup());

describe("资源状态图标", () => {
  it("显示 store 中的 CPU 与内存百分比，紧凑取整", () => {
    useSystemStore.setState({ cpuPercent: 12.5, memoryPercent: 34.6 });

    render(<ResourceIcon />);

    expect(screen.getByTestId("resource-values").textContent).toBe(
      "13% 35%",
    );
    expect(screen.getByTestId("status-resource").getAttribute("title")).toBe(
      "CPU 13%，内存 35%",
    );
  });

  it("每秒推送的新值直接刷新显示，无需组件自己轮询", () => {
    const view = render(<ResourceIcon />);
    expect(screen.getByTestId("resource-values").textContent).toBe("0% 0%");

    act(() => {
      useSystemStore.getState().setResourceUsage(8.2, 61.4);
    });

    expect(screen.getByTestId("resource-values").textContent).toBe("8% 61%");
    view.unmount();
  });
});
