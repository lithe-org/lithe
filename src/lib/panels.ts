import { showPopup } from "./api";

/** 控制中心五个模块的稳定标识，前端与面板注入共用。 */
export type ControlModule =
  | "network"
  | "volume"
  | "battery"
  | "resource"
  | "theme";

export function openControlCenter(element: HTMLElement, focus: ControlModule) {
  const rect = element.getBoundingClientRect();
  void showPopup(
    { x: rect.left, y: rect.top, width: rect.width, height: rect.height },
    "control-center",
    focus,
  );
}
