import { create } from "zustand";

export type ThemeMode = "light" | "dark" | "system";

export interface ThemeInfo {
  dark: boolean;
  accent: string;
}

interface ThemeState extends ThemeInfo {
  mode: ThemeMode;
  setMode: (mode: ThemeMode) => void;
  applySystemTheme: (info: ThemeInfo) => void;
}

const STORAGE_KEY = "lithe.theme-mode";

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

/** 亮/暗由模式决定；跟随系统时用系统当前值。 */
export function resolveDark(state: Pick<ThemeState, "mode" | "dark">): boolean {
  return state.mode === "dark" || (state.mode === "system" && state.dark);
}

export const useThemeStore = create<ThemeState>((set) => ({
  mode: readStoredMode(),
  dark: false,
  accent: "#0078D4",
  setMode: (mode) => {
    try {
      window.localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // 存不下也不影响本次会话生效。
    }
    set({ mode });
  },
  applySystemTheme: ({ dark, accent }) => set({ dark, accent }),
}));
