import { closePopup, openNotificationCenter } from "./api";

/**
 * 顶栏的通知与时间入口：先收起 Lithe 自己的面板，再把 Win+N 交给系统。
 * 顶栏是 NOACTIVATE 窗口，点击它不会让面板失焦，所以必须显式关闭。
 */
export async function openNotifications() {
  try {
    await closePopup();
  } catch {
    // 没有面板可收时也继续发送快捷键。
  }
  try {
    await openNotificationCenter();
  } catch {
    // 系统拒绝模拟按键时静默失败，不弹对话框打断用户。
  }
}
