import { create } from "zustand";

export interface VisibilitySettings {
  resource: boolean;
  wifi: boolean;
  volume: boolean;
  battery: boolean;
  datetime: boolean;
}

interface SettingsState {
  visibility: VisibilitySettings;
  barHeight: number;
  autoHideFullscreen: boolean;
  doubleClickOnEmpty: boolean;
  toggleVisibility: (key: keyof VisibilitySettings) => void;
  setBarHeight: (height: number) => void;
  setAutoHideFullscreen: (enabled: boolean) => void;
  setDoubleClickOnEmpty: (enabled: boolean) => void;
}

const STORAGE_KEY = "lithe.settings";

export const BAR_HEIGHT_MIN = 28;
export const BAR_HEIGHT_MAX = 36;

interface StoredSettings {
  visibility: VisibilitySettings;
  barHeight: number;
  autoHideFullscreen: boolean;
  doubleClickOnEmpty: boolean;
}

/** 设计基线 28~36，越界值按最近端点处理，避免撑破顶栏布局。 */
export function clampBarHeight(value: number): number {
  if (!Number.isFinite(value)) {
    return 30;
  }
  return Math.min(BAR_HEIGHT_MAX, Math.max(BAR_HEIGHT_MIN, Math.round(value)));
}

const DEFAULTS: StoredSettings = {
  visibility: {
    resource: true,
    wifi: true,
    volume: true,
    battery: true,
    datetime: true,
  },
  barHeight: 30,
  autoHideFullscreen: true,
  doubleClickOnEmpty: false,
};

function readStored(): StoredSettings {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return DEFAULTS;
    }
    const saved = JSON.parse(raw) as Partial<StoredSettings>;
    return {
      visibility: { ...DEFAULTS.visibility, ...saved.visibility },
      barHeight: clampBarHeight(saved.barHeight ?? DEFAULTS.barHeight),
      autoHideFullscreen: saved.autoHideFullscreen ?? true,
      doubleClickOnEmpty: saved.doubleClickOnEmpty ?? false,
    };
  } catch {
    // 存储损坏或不可用时退回默认值，不让设置窗口打不开。
    return DEFAULTS;
  }
}

function persist(settings: StoredSettings) {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
  } catch {
    // 隐私模式下忽略持久化失败。
  }
}

export const useSettingsStore = create<SettingsState>((set, get) => ({
  ...readStored(),
  toggleVisibility: (key) => {
    const state = get();
    const next = {
      ...state,
      visibility: {
        ...state.visibility,
        [key]: !state.visibility[key],
      },
    };
    persist(next);
    set(next);
  },
  setBarHeight: (height) => {
    const next = { ...get(), barHeight: clampBarHeight(height) };
    persist(next);
    set(next);
  },
  setAutoHideFullscreen: (enabled) => {
    const next = { ...get(), autoHideFullscreen: enabled };
    persist(next);
    set(next);
  },
  setDoubleClickOnEmpty: (enabled) => {
    const next = { ...get(), doubleClickOnEmpty: enabled };
    persist(next);
    set(next);
  },
}));
