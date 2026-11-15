import { useEffect } from "react";
import type { UnlistenFn } from "@tauri-apps/api/event";
import { getForegroundApp } from "./lib/api";
import { tickClock } from "./lib/clock";
import { registerSystemEvents } from "./lib/events";
import { useSystemStore } from "./stores/system";

function App() {
  const foregroundApp = useSystemStore((state) => state.foregroundApp);
  const setForegroundApp = useSystemStore((state) => state.setForegroundApp);
  const time = useSystemStore((state) => state.time);

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

  useEffect(() => {
    // 每秒检查一次，但 minute 未变时不写 store，顶栏不会跟着秒针重渲染。
    const tick = () => {
      const { time: current, setTime } = useSystemStore.getState();
      tickClock(current, new Date(), setTime);
    };
    tick();
    const timer = window.setInterval(tick, 1000);
    return () => window.clearInterval(timer);
  }, []);

  return (
    <div
      className="lithe-root grid h-[var(--lithe-bar-height)] w-full grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-[var(--lithe-icon-gap)] px-[var(--lithe-bar-padding-x)]"
      data-testid="lithe-root"
    >
      <section
        className="flex min-w-0 items-center gap-[var(--lithe-icon-gap)]"
        data-testid="zone-left"
      >
        {/* M2-03 替换为 SVG 四格 logo；先用方块占位，避免引入系统字符图标。 */}
        <span
          aria-hidden="true"
          className="block size-[var(--lithe-icon-size)] shrink-0 rounded-[2px] bg-current opacity-80"
          data-testid="windows-logo-placeholder"
        />
        {/* M2 起按 PRD 5.2.2 改为区域内横向滚动，骨架阶段先截断以免撑破布局。 */}
        <span className="min-w-0 truncate" data-testid="foreground-app">
          {foregroundApp}
        </span>
      </section>

      <section
        className="flex items-center gap-[var(--lithe-icon-gap)]"
        data-testid="zone-center"
      />

      <section
        className="flex items-center justify-end gap-[var(--lithe-icon-gap)]"
        data-testid="zone-right"
      >
        <time className="tabular-nums" data-testid="clock">
          {time || "--:--"}
        </time>
      </section>
    </div>
  );
}

export default App;
