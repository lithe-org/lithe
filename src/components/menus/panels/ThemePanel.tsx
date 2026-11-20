import { Monitor, Moon, RotateCcw, Sun } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ACCENT_PRESETS, useThemeStore } from "../../../stores/theme";
import type { ThemeMode } from "../../../stores/theme";

const MODES: { id: ThemeMode; key: string; icon: LucideIcon }[] = [
  { id: "light", key: "light", icon: Sun },
  { id: "dark", key: "dark", icon: Moon },
  { id: "system", key: "system", icon: Monitor },
];

/** PRD 5.4.3 的主题模块：三档模式 + 预设色 + 自定义 hex。 */
export function ThemePanel() {
  const mode = useThemeStore((state) => state.mode);
  const accent = useThemeStore((state) => state.accent);
  const setMode = useThemeStore((state) => state.setMode);
  const setAccent = useThemeStore((state) => state.setAccent);
  const resetAccent = useThemeStore((state) => state.resetAccent);
  const [draft, setDraft] = useState(accent);
  const { t } = useTranslation();

  return (
    <div className="text-[11px]" data-testid="theme-panel">
      <div aria-label={t("theme.mode")} className="flex gap-1" role="group">
        {MODES.map(({ id, key, icon: Icon }) => (
          <button
            aria-label={t(`theme.${key}`)}
            aria-pressed={mode === id}
            className="flex items-center gap-1 rounded-[var(--lithe-radius)] px-2 py-1 hover:bg-black/10 aria-pressed:bg-[var(--lithe-accent)] aria-pressed:text-white"
            data-testid={`theme-mode-${id}`}
            key={id}
            onClick={() => setMode(id)}
            type="button"
          >
            <Icon className="size-3" />
            {t(`theme.${key}`)}
          </button>
        ))}
      </div>

      <div
        aria-label={t("theme.accent")}
        className="mt-2 flex items-center gap-1"
        role="group"
      >
        {ACCENT_PRESETS.map((color) => (
          <button
            aria-label={t("theme.accentPreset", { color })}
            className="size-4 rounded-full border border-black/10"
            data-testid={`accent-${color}`}
            key={color}
            onClick={() => {
              setDraft(color);
              setAccent(color);
            }}
            style={{ background: color }}
            type="button"
          />
        ))}
      </div>

      <div className="mt-2 flex items-center gap-2">
        <input
          aria-label={t("theme.customAccent")}
          className="w-24 rounded-[var(--lithe-radius)] border border-black/10 bg-transparent px-1 py-0.5 font-mono"
          data-testid="accent-hex"
          onChange={(event) => {
            const value = event.currentTarget.value;
            setDraft(value);
            setAccent(value);
          }}
          spellCheck={false}
          value={draft}
        />
        <button
          aria-label={t("theme.resetAccent")}
          className="rounded-[var(--lithe-radius)] p-1 hover:bg-black/10"
          data-testid="accent-reset"
          onClick={() => {
            resetAccent();
            setDraft(useThemeStore.getState().accent);
          }}
          type="button"
        >
          <RotateCcw className="size-3" />
        </button>
      </div>

      <p className="lithe-accent-text mt-1" data-testid="accent-current">
        {t("theme.currentAccent", { accent })}
      </p>
    </div>
  );
}
