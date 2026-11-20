import { create } from "zustand";

export type ThemeMode = "light" | "dark" | "system";

export interface ThemeInfo {
  dark: boolean;
  accent: string;
}

interface ThemeState extends ThemeInfo {
  mode: ThemeMode;
  /** null 表示跟随系统强调色。 */
  accentOverride: string | null;
  systemAccent: string;
  setMode: (mode: ThemeMode) => void;
  setAccent: (accent: string) => void;
  resetAccent: () => void;
  applySystemTheme: (info: ThemeInfo) => void;
}

const STORAGE_KEY = "lithe.theme-mode";
const ACCENT_KEY = "lithe.accent";
const HEX_COLOR = /^#[0-9a-fA-F]{6}$/;

export const ACCENT_PRESETS = [
  "#0078D4",
  "#0F6CBD",
  "#2563EB",
  "#7B2CBF",
  "#D13438",
  "#107C10",
];

function readStoredMode(): ThemeMode {
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY);
    if (saved === "light" || saved === "dark" || saved === "system") {
      return saved;
    }
  } catch {
    // 隐私模式下 localStorage 不可用，退回跟随系统。
  }
  return "system";
}

function readAccentOverride(): string | null {
  try {
    const saved = window.localStorage.getItem(ACCENT_KEY);
    return saved && HEX_COLOR.test(saved) ? saved : null;
  } catch {
    return null;
  }
}

/** 亮/暗由模式决定；跟随系统时用系统当前值。 */
export function resolveDark(state: Pick<ThemeState, "mode" | "dark">): boolean {
  return state.mode === "dark" || (state.mode === "system" && state.dark);
}

export const useThemeStore = create<ThemeState>((set, get) => ({
  mode: readStoredMode(),
  dark: false,
  systemAccent: "#0078D4",
  accentOverride: readAccentOverride(),
  accent: readAccentOverride() ?? "#0078D4",
  setMode: (mode) => {
    try {
      window.localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // 存不下也不影响本次会话生效。
    }
    set({ mode });
  },
  setAccent: (accent) => {
    if (!HEX_COLOR.test(accent)) {
      return;
    }
    try {
      window.localStorage.setItem(ACCENT_KEY, accent);
    } catch {
      // 存不下也不影响本次会话生效。
    }
    set({ accent, accentOverride: accent });
  },
  resetAccent: () => {
    try {
      window.localStorage.removeItem(ACCENT_KEY);
    } catch {
      // 忽略存储失败。
    }
    set({ accentOverride: null, accent: get().systemAccent });
  },
  applySystemTheme: ({ dark, accent }) => {
    // 用户自定义过强调色时，系统主题变化不能覆盖它。
    set({
      dark,
      systemAccent: accent,
      accent: get().accentOverride ?? accent,
    });
  },
}));
