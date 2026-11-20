import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import enUS from "../locales/en-US.json";
import zhCN from "../locales/zh-CN.json";

export const LANGUAGE_KEY = "lithe.language";
export type Language = "zh-CN" | "en-US";

function storedLanguage(): Language {
  try {
    return window.localStorage.getItem(LANGUAGE_KEY) === "en-US"
      ? "en-US"
      : "zh-CN";
  } catch {
    // 隐私模式下 localStorage 不可用时默认中文。
    return "zh-CN";
  }
}

i18n.use(initReactI18next).init({
  resources: {
    "zh-CN": { translation: zhCN },
    "en-US": { translation: enUS },
  },
  lng: storedLanguage(),
  fallbackLng: "zh-CN",
  interpolation: { escapeValue: false },
  // 语言包随 bundle 一起加载，不需要 Suspense 边界。
  react: { useSuspense: false },
});

export function setLanguage(language: Language) {
  void i18n.changeLanguage(language);
  try {
    window.localStorage.setItem(LANGUAGE_KEY, language);
  } catch {
    // 存不下也不影响本次会话。
  }
}

export default i18n;
