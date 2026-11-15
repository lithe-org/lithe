/** 顶栏与下拉窗口共用同一份前端 bundle，靠 label 前缀区分渲染内容。 */
export const POPUP_LABEL_PREFIX = "popup-";

export function isPopupLabel(label: string): boolean {
  return label.startsWith(POPUP_LABEL_PREFIX);
}
