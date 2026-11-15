import { create } from "zustand";

/** 系统状态快照：字段先占位，M1-05 / M3 起由 Rust 侧事件填充。 */
export interface SystemSnapshot {
  foregroundApp: string;
  time: string;
  cpuPercent: number;
  memoryPercent: number;
  wifi: { connected: boolean; ssid: string; signal: number };
  volume: { level: number; muted: boolean };
  battery: { present: boolean; percent: number; charging: boolean };
}

export interface SystemState extends SystemSnapshot {
  setForegroundApp: (name: string) => void;
  setTime: (time: string) => void;
  setResourceUsage: (cpuPercent: number, memoryPercent: number) => void;
  patch: (partial: Partial<SystemSnapshot>) => void;
}

export const initialSystemSnapshot: SystemSnapshot = {
  foregroundApp: "应用",
  time: "",
  cpuPercent: 0,
  memoryPercent: 0,
  wifi: { connected: false, ssid: "", signal: 0 },
  volume: { level: 0, muted: false },
  battery: { present: false, percent: 0, charging: false },
};

export const useSystemStore = create<SystemState>((set) => ({
  ...initialSystemSnapshot,
  setForegroundApp: (foregroundApp) => set({ foregroundApp }),
  setTime: (time) => set({ time }),
  setResourceUsage: (cpuPercent, memoryPercent) => set({ cpuPercent, memoryPercent }),
  patch: (partial) => set(partial),
}));
