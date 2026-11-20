import { resolveDark, useThemeStore } from "../stores/theme";

const LIGHT = {
  tint: "rgba(255, 255, 255, 0.32)",
  fg: "rgba(18, 18, 20, 0.92)",
};
const DARK = {
  tint: "rgba(18, 18, 20, 0.62)",
  fg: "rgba(245, 245, 245, 0.94)",
};

/** CSS 变量与 Tailwind dark 类是主题的单一出口，切换只走这里。 */
export function applyTheme() {
  const { mode, dark, accent } = useThemeStore.getState();
  const night = resolveDark({ mode, dark });
  const palette = night ? DARK : LIGHT;
  const root = document.documentElement;

  root.classList.toggle("dark", night);
  root.style.setProperty("--lithe-bar-tint", palette.tint);
  root.style.setProperty("--lithe-bar-fg", palette.fg);
  root.style.setProperty("--lithe-accent", accent);
}
