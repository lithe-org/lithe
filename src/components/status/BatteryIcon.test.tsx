import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../../stores/system";
import { BatteryIcon } from "./BatteryIcon";

beforeEach(() => {
  useSystemStore.setState(initialSystemSnapshot);
});

afterEach(() => cleanup());

const iconClass = () =>
  screen
    .getByTestId("status-battery")
    .querySelector("svg")
    ?.getAttribute("class") ?? "";

function setBattery(percent: number, charging = false, present = true) {
  act(() => {
    useSystemStore.getState().patch({
      battery: { present, percent, charging },
    });
  });
}

describe("电池状态图标", () => {
  it("台式机无电池时完全隐藏", () => {
    const { container } = render(<BatteryIcon />);

    expect(screen.queryByTestId("status-battery")).toBeNull();
    expect(container.childElementCount).toBe(0);
  });

  it("充电状态优先于电量档位", () => {
    const view = render(<BatteryIcon />);
    setBattery(85, true);

    expect(iconClass()).toContain("lucide-battery-charging");
    expect(screen.getByTestId("status-battery").getAttribute("title")).toBe(
      "电池充电中 85%",
    );
    view.unmount();
  });

  it("未充电时按电量显示警告、低、中、满四档", () => {
    const view = render(<BatteryIcon />);

    setBattery(8);
    expect(iconClass()).toContain("lucide-battery-warning");

    setBattery(25);
    expect(iconClass()).toContain("lucide-battery-low");

    setBattery(55);
    expect(iconClass()).toContain("lucide-battery-medium");

    setBattery(95);
    expect(iconClass()).toContain("lucide-battery-full");
    view.unmount();
  });
});
