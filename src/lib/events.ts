import { listen, type UnlistenFn } from "@tauri-apps/api/event";
import type {
  ForegroundApp,
  ResourceUsage,
  VolumeStatus,
  WifiStatus,
} from "./api";
import { useSystemStore } from "../stores/system";

// 与 src-tauri/src/commands/mod.rs 的常量逐字一致，由 api.test.ts 校验漂移。
// 事件订阅统一在本文件注册，组件不直接引用事件名字符串。
export const EVENTS = {
  foregroundChanged: "system:foreground-changed",
  statusTick: "system:status-tick",
  resourceTick: "system:resource-tick",
  wifiChanged: "system:wifi-changed",
  volumeChanged: "system:volume-changed",
  batteryChanged: "system:battery-changed",
  popupShow: "popup:show",
} as const;

/** 注册系统事件订阅，返回卸载函数供组件卸载时调用，避免重复订阅。 */
export function registerSystemEvents(): Promise<UnlistenFn[]> {
  return Promise.all([
    listen<ForegroundApp>(EVENTS.foregroundChanged, (event) => {
      const { setForegroundApp, setForegroundControllable } =
        useSystemStore.getState();
      setForegroundApp(event.payload.name);
      setForegroundControllable(event.payload.controllable);
    }),
    listen<ResourceUsage>(EVENTS.resourceTick, (event) => {
      const { setResourceUsage } = useSystemStore.getState();
      setResourceUsage(event.payload.cpuPercent, event.payload.memoryPercent);
    }),
    listen<WifiStatus>(EVENTS.wifiChanged, (event) => {
      useSystemStore.getState().patch({ wifi: event.payload });
    }),
    listen<VolumeStatus>(EVENTS.volumeChanged, (event) => {
      useSystemStore.getState().patch({ volume: event.payload });
    }),
  ]);
}
