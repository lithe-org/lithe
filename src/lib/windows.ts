import { WebviewWindow } from "@tauri-apps/api/webviewWindow";
import { SETTINGS_LABEL } from "./windowRole";

/** 设置窗口在 tauri.conf.json 里预创建且隐藏，这里只负责显示。 */
export async function showSettingsWindow() {
  const window = await WebviewWindow.getByLabel(SETTINGS_LABEL);
  if (window) {
    await window.show();
    await window.setFocus();
  }
}
