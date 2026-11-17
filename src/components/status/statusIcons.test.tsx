import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import {
  BatteryIcon,
  ClockDisplay,
  NotificationIcon,
  ResourceIcon,
  VolumeIcon,
  WifiIcon,
} from "./index";

afterEach(() => cleanup());

function StatusRow() {
  return (
    <div data-testid="icons-row">
      <ResourceIcon />
      <WifiIcon />
      <VolumeIcon />
      <BatteryIcon />
      <NotificationIcon />
      <ClockDisplay time="09:05" />
    </div>
  );
}

describe("右侧状态图标", () => {
  it("按 PRD 5.4.2 顺序渲染资源、Wi-Fi、音量、电池、通知、时间", () => {
    const { container } = render(<StatusRow />);
    const ids = Array.from(
      container.querySelectorAll('[data-testid^="status-"]'),
    ).map((node) => node.getAttribute("data-testid"));

    expect(ids).toEqual([
      "status-resource",
      "status-wifi",
      "status-volume",
      "status-battery",
      "status-notification",
    ]);
    expect(screen.getByTestId("clock").textContent).toBe("09:05");
  });

  it("图标尺寸统一为设计 token，颜色继承前景色", () => {
    render(<StatusRow />);

    for (const id of [
      "status-resource",
      "status-wifi",
      "status-volume",
      "status-battery",
      "status-notification",
    ]) {
      const item = screen.getByTestId(id);
      expect(item.getAttribute("class")).toContain(
        "text-[var(--lithe-bar-fg)]",
      );
      expect(item.querySelector("svg")?.getAttribute("class")).toContain(
        "size-[var(--lithe-icon-size)]",
      );
    }
  });

  it("资源图标先显示占位百分比，真实数值由 M3-03 接入", () => {
    render(<StatusRow />);
    expect(screen.getByTestId("resource-values").textContent).toBe("0% 0%");
  });
});
