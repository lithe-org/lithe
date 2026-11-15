import { invoke } from "@tauri-apps/api/core";
import { useEffect } from "react";

/** 下拉窗口的宿主容器；M2-04 起按事件注入 Logo 菜单等面板内容。 */
export function PopupHost() {
  useEffect(() => {
    // 面板内按 Esc 收起；点击外部由 Rust 侧的失焦事件处理。
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        void invoke("hide_popup");
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  return (
    <div
      className="h-full w-full bg-[var(--lithe-bar-tint)]"
      data-testid="popup-host"
    />
  );
}
