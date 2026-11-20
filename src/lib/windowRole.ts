/** 顶栏、下拉与设置窗口共用同一份前端 bundle，靠 label 区分渲染内容。 */
export const POPUP_LABEL_PREFIX = "popup-";
export const SETTINGS_LABEL = "settings";

export function isSettingsLabel(label: string): boolean {
  return label === SETTINGS_LABEL;
}

export function isPopupLabel(label: string): boolean {
  return label.startsWith(POPUP_LABEL_PREFIX);
}
