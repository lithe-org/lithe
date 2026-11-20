import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { useSettingsStore } from "../../../stores/settings";
import { StatusBarPage } from "./StatusBarPage";

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

describe("状态栏设置", () => {
  it("五个开关控制各状态项显示", () => {
    render(<StatusBarPage />);

    fireEvent.click(screen.getByTestId("visibility-resource"));
    expect(useSettingsStore.getState().visibility.resource).toBe(false);
    expect(
      (screen.getByTestId("visibility-resource") as HTMLInputElement).checked,
    ).toBe(false);

    fireEvent.click(screen.getByTestId("visibility-datetime"));
    expect(useSettingsStore.getState().visibility.datetime).toBe(false);
  });

  it("高度调节写入 store 并回显当前值", () => {
    render(<StatusBarPage />);

    fireEvent.change(screen.getByTestId("bar-height"), {
      target: { value: "34" },
    });

    expect(useSettingsStore.getState().barHeight).toBe(34);
    expect(screen.getByTestId("bar-height-value").textContent).toBe("34px");
  });
});
