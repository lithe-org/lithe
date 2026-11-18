import { invoke } from "@tauri-apps/api/core";
import { listen, type UnlistenFn } from "@tauri-apps/api/event";
import { useEffect, useState } from "react";
import { EVENTS } from "../lib/events";
import type { ControlModule } from "../lib/panels";
import { ControlCenter } from "./menus/ControlCenter";
import { LogoMenu } from "./menus/LogoMenu";
import { SplitMenu } from "./menus/SplitMenu";

/** 下拉窗口的宿主容器；M2-04 起按事件注入 Logo 菜单等面板内容。 */
export function PopupHost() {
  const [kind, setKind] = useState<string | null>(null);
  const [focus, setFocus] = useState<ControlModule | undefined>(undefined);

  useEffect(() => {
    // 池中窗口复用，内容类型由 Rust 在每次 show 时注入。
    let cancelled = false;
    let unlisten: UnlistenFn | undefined;
    listen<{ kind: string; focus?: ControlModule }>(
      EVENTS.popupShow,
      (event) => {
        setKind(event.payload.kind);
        setFocus(event.payload.focus);
      },
    )
      .then((fn) => {
        if (cancelled) {
          fn();
        } else {
          unlisten = fn;
        }
      })
      .catch(() => {
        // 订阅失败时保持空面板，不阻塞窗口显示。
      });
    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);

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
    >
      {kind === "logo" ? <LogoMenu /> : null}
      {kind === "control-center" ? <ControlCenter focus={focus} /> : null}
      {kind === "split" ? <SplitMenu /> : null}
    </div>
  );
}
