import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { useSettingsStore } from "../../../stores/settings";
import { BehaviorPage } from "./BehaviorPage";

beforeEach(() => {
  window.localStorage.clear();
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

afterEach(() => cleanup());

describe("行为设置", () => {
  it("全屏自动隐藏与双击行为开关写入设置", () => {
    render(<BehaviorPage />);

    fireEvent.click(screen.getByTestId("behavior-autohide"));
    expect(useSettingsStore.getState().autoHideFullscreen).toBe(false);

    fireEvent.click(screen.getByTestId("behavior-double-click"));
    expect(useSettingsStore.getState().doubleClickOnEmpty).toBe(true);
  });
});
