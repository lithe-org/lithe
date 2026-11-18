import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { initialSystemSnapshot, useSystemStore } from "../../stores/system";
import { VolumeIcon } from "./VolumeIcon";

beforeEach(() => {
  useSystemStore.setState(initialSystemSnapshot);
});

afterEach(() => cleanup());

const iconClass = () =>
  screen.getByTestId("status-volume").querySelector("svg")?.getAttribute("class") ?? "";

const iconTokens = () => iconClass().split(/\s+/);

function setVolume(level: number, muted = false) {
  act(() => {
    useSystemStore.getState().patch({ volume: { level, muted } });
  });
}

describe("音量状态图标", () => {
  it("静音或 0% 显示 volume-x", () => {
    const view = render(<VolumeIcon />);
    expect(iconClass()).toContain("lucide-volume-x");

    setVolume(80, true);
    expect(iconClass()).toContain("lucide-volume-x");
    expect(screen.getByTestId("status-volume").getAttribute("title")).toBe(
      "音量已静音",
    );
    view.unmount();
  });

  it("按音量分三档显示 volume / volume-1 / volume-2", () => {
    const view = render(<VolumeIcon />);

    setVolume(20);
    expect(iconTokens()).toContain("lucide-volume");

    setVolume(50);
    expect(iconClass()).toContain("lucide-volume-1");

    setVolume(90);
    expect(iconClass()).toContain("lucide-volume-2");
    view.unmount();
  });
});
