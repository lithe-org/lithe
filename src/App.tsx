import { useEffect } from "react";
import type { UnlistenFn } from "@tauri-apps/api/event";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { PopupHost } from "./components/PopupHost";
import { WindowControls } from "./components/WindowControls";
import {
  BatteryIcon,
  ClockDisplay,
  NotificationIcon,
  ResourceIcon,
  VolumeIcon,
  WifiIcon,
} from "./components/status";
import { WindowsLogo } from "./components/WindowsLogo";
import { getForegroundApp, showPopup } from "./lib/api";
import { isPopupLabel } from "./lib/windowRole";
import { tickClock } from "./lib/clock";
import { registerSystemEvents } from "./lib/events";
import { useSystemStore } from "./stores/system";

async function openLogoMenu(element: HTMLElement) {
  const rect = element.getBoundingClientRect();
  // 落点交给 Rust 计算，超出屏幕时会翻转或平移。
  await showPopup(
    {
      x: rect.left,
      y: rect.top,
      width: rect.width,
      height: rect.height,
    },
    "logo",
  );
}

function StatusBar() {
  const foregroundApp = useSystemStore((state) => state.foregroundApp);
  const foregroundControllable = useSystemStore(
    (state) => state.foregroundControllable,
  );
  const setForegroundApp = useSystemStore((state) => state.setForegroundApp);
  const setForegroundControllable = useSystemStore(
    (state) => state.setForegroundControllable,
  );
  const time = useSystemStore((state) => state.time);

  useEffect(() => {
    let cancelled = false;
    getForegroundApp()
      .then((app) => {
        if (!cancelled) {
          setForegroundApp(app.name);
          setForegroundControllable(app.controllable);
        }
      })
      .catch(() => {
        // IPC 不可用时保留占位文案，不让顶栏启动被一次查询打断。
      });
    return () => {
      cancelled = true;
    };
  }, [setForegroundApp, setForegroundControllable]);

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
      className="lithe-root grid h-[var(--lithe-bar-height)] w-full grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-[var(--lithe-icon-gap)] bg-[var(--lithe-bar-tint)] px-[var(--lithe-bar-padding-x)] text-[var(--lithe-bar-fg)]"
      data-testid="lithe-root"
    >
      <section
        className="flex min-w-0 items-center gap-[var(--lithe-icon-gap)]"
        data-testid="zone-left"
      >
        <button
          aria-label="Lithe 菜单"
          className="flex shrink-0 items-center rounded-[var(--lithe-radius)] p-0.5 hover:bg-black/10"
          data-testid="windows-logo-button"
          onClick={(event) => void openLogoMenu(event.currentTarget)}
          type="button"
        >
          <WindowsLogo />
        </button>
        {/* M2 起按 PRD 5.2.2 改为区域内横向滚动，骨架阶段先截断以免撑破布局。 */}
        <span className="min-w-0 truncate" data-testid="foreground-app">
          {foregroundApp}
        </span>
      </section>

      <section
        className="flex items-center gap-[var(--lithe-icon-gap)]"
        data-testid="zone-center"
      >
        {foregroundControllable ? <WindowControls /> : null}
      </section>

      <section
        className="flex items-center justify-end gap-[var(--lithe-icon-gap)]"
        data-testid="zone-right"
      >
        <ResourceIcon />
        <WifiIcon />
        <VolumeIcon />
        <BatteryIcon />
        <NotificationIcon />
        <ClockDisplay time={time} />
      </section>
    </div>
  );
}

function App() {
  // 顶栏与下拉窗口共用同一份 bundle，按窗口 label 分流后各自的 hooks 互不影响。
  return isPopupLabel(getCurrentWindow().label) ? <PopupHost /> : <StatusBar />;
}

export default App;
