import { describe, expect, it } from "vitest";
import { isPopupLabel, POPUP_LABEL_PREFIX } from "./windowRole";

describe("窗口角色判定", () => {
  it("按 label 前缀区分下拉窗口与顶栏", () => {
    expect(isPopupLabel(`${POPUP_LABEL_PREFIX}0`)).toBe(true);
    expect(isPopupLabel("popup-1")).toBe(true);
  });

  it("顶栏 main 与其它 label 不被误判为下拉窗口", () => {
    expect(isPopupLabel("main")).toBe(false);
    expect(isPopupLabel("settings")).toBe(false);
    expect(isPopupLabel("")).toBe(false);
  });
});
