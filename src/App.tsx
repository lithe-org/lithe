import { useEffect } from "react";
import "./lib/i18n";
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
import {
  getForegroundApp,
  getThemeInfo,
  setFullscreenAutohide,
  showPopup,
  toggleMaximize,
} from "./lib/api";
import { isPopupLabel, isSettingsLabel } from "./lib/windowRole";
import { SettingsWindow } from "./components/settings/SettingsWindow";
import { tickClock } from "./lib/clock";
import { registerSystemEvents } from "./lib/events";
import { useSystemStore } from "./stores/system";
import { useThemeStore } from "./stores/theme";
import { useSettingsStore } from "./stores/settings";
import { applyBarHeight } from "./lib/settings";
import { useTranslation } from "react-i18next";
import { applyTheme } from "./lib/theme";

/** 顶栏与下拉窗口都需要同一份主题；变化时统一写 CSS 变量与 dark 类。 */
function ThemeSync() {
  const mode = useThemeStore((state) => state.mode);
  const dark = useThemeStore((state) => state.dark);
  const accent = useThemeStore((state) => state.accent);
  const barHeight = useSettingsStore((state) => state.barHeight);
  const autoHideFullscreen = useSettingsStore(
    (state) => state.autoHideFullscreen,
  );

  useEffect(() => {
    let cancelled = false;
    getThemeInfo()
      .then((info) => {
        if (!cancelled && info) {
          useThemeStore.getState().applySystemTheme(info);
        }
      })
      .catch(() => {
        // 读不到系统主题时保留默认值，手动切换仍可用。
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    applyTheme();
  }, [mode, dark, accent]);

  useEffect(() => {
    applyBarHeight(barHeight);
  }, [barHeight]);

  useEffect(() => {
    // 设置窗口与顶栏是同一 bundle 的不同窗口，Rust 侧开关以任一窗口的设置为准。
    void setFullscreenAutohide(autoHideFullscreen).catch(() => {});
  }, [autoHideFullscreen]);

  return null;
}

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
  const { t, i18n } = useTranslation();
  const clockLanguage = i18n.language === "en-US" ? "en-US" : "zh-CN";
  const visibility = useSettingsStore((state) => state.visibility);
  const doubleClickOnEmpty = useSettingsStore(
    (state) => state.doubleClickOnEmpty,
  );

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
      tickClock(current, new Date(), setTime, clockLanguage);
    };
    tick();
    const timer = window.setInterval(tick, 1000);
    return () => window.clearInterval(timer);
  }, [clockLanguage]);

  return (
    <div
      className="lithe-root grid h-[var(--lithe-bar-height)] w-full grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-[var(--lithe-icon-gap)] bg-[var(--lithe-bar-tint)] px-[var(--lithe-bar-padding-x)] text-[var(--lithe-bar-fg)]"
      data-testid="lithe-root"
      onDoubleClick={(event) => {
        // NOACTIVATE 顶栏不会抢焦点，双击空白区域操作的是当前前台窗口。
        if (doubleClickOnEmpty && event.target === event.currentTarget) {
          void toggleMaximize();
        }
      }}
    >
      <section
        className="flex min-w-0 items-center gap-[var(--lithe-icon-gap)]"
        data-testid="zone-left"
      >
        <button
          aria-label={t("app.litheMenu")}
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
        {visibility.resource ? <ResourceIcon /> : null}
        {visibility.wifi ? <WifiIcon /> : null}
        {visibility.volume ? <VolumeIcon /> : null}
        {visibility.battery ? <BatteryIcon /> : null}
        <NotificationIcon />
        {visibility.datetime ? <ClockDisplay time={time} /> : null}
      </section>
    </div>
  );
}

function App() {
  // 顶栏与下拉窗口共用同一份 bundle，按窗口 label 分流后各自的 hooks 互不影响。
  const label = getCurrentWindow().label;
  const content = isSettingsLabel(label) ? (
    <SettingsWindow />
  ) : isPopupLabel(label) ? (
    <PopupHost />
  ) : (
    <StatusBar />
  );
  return (
    <>
      <ThemeSync />
      {content}
    </>
  );
}

export default App;
