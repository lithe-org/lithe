import { create } from "zustand";

export type ThemeMode = "light" | "dark" | "system";
export type Language = "zh-CN" | "en-US";
/** 面板开关先占位，M2 起由下拉窗口池接管实际的显示与归还。 */
export type PanelKind = "logo" | "control-center" | "split" | null;

export interface UiSnapshot {
  theme: ThemeMode;
  language: Language;
  openPanel: PanelKind;
}

export interface UiState extends UiSnapshot {
  setTheme: (theme: ThemeMode) => void;
  setLanguage: (language: Language) => void;
  setOpenPanel: (panel: PanelKind) => void;
}

// PRD 6.2 / 7：主题默认跟随系统，界面语言默认中文且不随系统语言变。
export const initialUiSnapshot: UiSnapshot = {
  theme: "system",
  language: "zh-CN",
  openPanel: null,
};

export const useUiStore = create<UiState>((set) => ({
  ...initialUiSnapshot,
  setTheme: (theme) => set({ theme }),
  setLanguage: (language) => set({ language }),
  setOpenPanel: (openPanel) => set({ openPanel }),
}));
