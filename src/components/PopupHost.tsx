import { invoke } from "@tauri-apps/api/core";
import { getPopupState } from "../lib/api";
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

    const applyState = (state: { kind: string; focus?: string } | null) => {
      if (!state || cancelled) {
        return;
      }
      setKind(state.kind);
      setFocus((state.focus as ControlModule | undefined) ?? undefined);
    };

    // 事件可能赶在 listener 注册前丢失，挂载后拉一次当前状态补上。
    const pull = () => {
      void getPopupState().then(applyState).catch(() => {});
    };

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
          pull();
        }
      })
      .catch(() => {
        // 订阅失败时仍靠拉取兜底。
      });

    // 显示的窗口持续低频拉取，彻底避免事件丢失留下空白面板；
    // 池里隐藏的窗口不轮询，不产生无谓 IPC。
    const timer = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        pull();
      }
    }, 150);

    return () => {
      cancelled = true;
      window.clearInterval(timer);
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
      className="h-full w-full bg-[var(--lithe-panel-tint)]"
      data-testid="popup-host"
    >
      {kind === "logo" ? <LogoMenu /> : null}
      {kind === "control-center" ? <ControlCenter focus={focus} /> : null}
      {kind === "split" ? <SplitMenu /> : null}
    </div>
  );
}
