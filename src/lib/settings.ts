import { setBarHeight } from "./api";

/** CSS 逻辑像素：顶栏各窗口按自己所在屏 DPI 换算物理高度（Rust 侧完成）。 */
export function applyBarHeight(height: number) {
  document.documentElement.style.setProperty(
    "--lithe-bar-height",
    `${height}px`,
  );
  void setBarHeight(height).catch(() => {
    // IPC 不可用时 CSS 仍然生效，顶栏不会卡在旧高度。
  });
}
