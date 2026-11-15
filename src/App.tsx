import { useEffect } from "react";
import type { UnlistenFn } from "@tauri-apps/api/event";
import { getForegroundApp } from "./lib/api";
import { registerSystemEvents } from "./lib/events";
import { useSystemStore } from "./stores/system";
import { useUiStore } from "./stores/ui";

function App() {
  // 占位文本只用于验证 store 订阅与前后端链路；M1-08 起替换为三区布局。
  const foregroundApp = useSystemStore((state) => state.foregroundApp);
  const setForegroundApp = useSystemStore((state) => state.setForegroundApp);
  const language = useUiStore((state) => state.language);

  useEffect(() => {
    let cancelled = false;
    getForegroundApp()
      .then((app) => {
        if (!cancelled) setForegroundApp(app.name);
      })
      .catch(() => {
        // IPC 不可用时保留占位文案，不让顶栏启动被一次查询打断。
      });
    return () => {
      cancelled = true;
    };
  }, [setForegroundApp]);

  useEffect(() => {
    // 依赖为空：订阅只建立一次，避免每次渲染解绑重绑导致事件丢失（坑 #27）。
    let cancelled = false;
    let unlisteners: UnlistenFn[] = [];
    registerSystemEvents()
      .then((fns) => {
        if (cancelled) {
          fns.forEach((fn) => fn());
        } else {
          unlisteners = fns;
        }
      })
      .catch(() => {
        // 订阅失败时保留启动拉取的占位文案，不阻塞顶栏。
      });
    return () => {
      cancelled = true;
      unlisteners.forEach((fn) => fn());
    };
  }, []);

  return (
    <div
      className="lithe-root flex h-[var(--lithe-bar-height)] w-full items-center gap-[var(--lithe-icon-gap)] px-[var(--lithe-bar-padding-x)]"
      data-testid="lithe-root"
    >
      <span data-testid="foreground-app">{foregroundApp}</span>
      <span data-testid="language">{language}</span>
    </div>
  );
}

export default App;
