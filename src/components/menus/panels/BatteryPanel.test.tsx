import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../../../stores/system";
import { BatteryPanel } from "./BatteryPanel";

beforeEach(() => {
  useSystemStore.setState(initialSystemSnapshot);
});

afterEach(() => cleanup());

describe("控制中心电池模块", () => {
  it("无电池时显示台式机提示而不是空白", () => {
    render(<BatteryPanel />);

    expect(screen.getByTestId("battery-summary").textContent).toBe(
      "无电池（台式机）",
    );
    expect(screen.queryByTestId("battery-state")).toBeNull();
  });

  it("有电池时显示电量与充电状态", () => {
    useSystemStore.setState({
      ...initialSystemSnapshot,
      battery: { present: true, percent: 77, charging: true },
    });
    render(<BatteryPanel />);

    expect(screen.getByTestId("battery-summary").textContent).toBe("77%");
    expect(screen.getByTestId("battery-state").textContent).toBe(
      "正在充电 / 外接电源",
    );
  });

  it("使用电池供电时文案区分于充电", () => {
    useSystemStore.setState({
      ...initialSystemSnapshot,
      battery: { present: true, percent: 55, charging: false },
    });
    render(<BatteryPanel />);

    expect(screen.getByTestId("battery-state").textContent).toBe(
      "使用电池供电",
    );
  });
});
