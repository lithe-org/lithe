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

export function AppearancePage() {
  const { t } = useTranslation();
  const mode = useThemeStore((state) => state.mode);
  const accent = useThemeStore((state) => state.accent);
  const setMode = useThemeStore((state) => state.setMode);
  const setAccent = useThemeStore((state) => state.setAccent);
  const resetAccent = useThemeStore((state) => state.resetAccent);
  const [draft, setDraft] = useState(accent);

  return (
    <div className="flex flex-col gap-5 text-[13px]" data-testid="appearance-page">
      <section>
        <p className="mb-2">{t("settings.appearance.theme")}</p>
        <div aria-label={t("settings.appearance.theme")} className="flex gap-2" role="group">
          {MODES.map(({ id, key, icon: Icon }) => (
            <button
              aria-pressed={mode === id}
              className="flex items-center gap-1 rounded-[var(--lithe-radius)] border border-black/10 px-3 py-1 aria-pressed:border-[var(--lithe-accent)] aria-pressed:text-[var(--lithe-accent)]"
              data-testid={`appearance-mode-${id}`}
              key={id}
              onClick={() => setMode(id)}
              type="button"
            >
              <Icon className="size-3" />
              {t(`theme.${key}`)}
            </button>
          ))}
        </div>
      </section>

      <section>
        <p className="mb-2">{t("settings.appearance.accent")}</p>
        <div aria-label={t("settings.appearance.accent")} className="flex items-center gap-2" role="group">
          {ACCENT_PRESETS.map((color) => (
            <button
              aria-label={t("theme.accentPreset", { color })}
              className="size-6 rounded-full border border-black/10"
              data-testid={`appearance-accent-${color}`}
              key={color}
              onClick={() => {
                setDraft(color);
                setAccent(color);
              }}
              style={{ background: color }}
              type="button"
            />
          ))}
          <input
            aria-label={t("theme.customAccent")}
            className="w-28 rounded-[var(--lithe-radius)] border border-black/10 bg-transparent px-2 py-1 font-mono"
            data-testid="appearance-accent-hex"
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
            data-testid="appearance-accent-reset"
            onClick={() => {
              resetAccent();
              setDraft(useThemeStore.getState().accent);
            }}
            type="button"
          >
            <RotateCcw className="size-3" />
          </button>
        </div>
      </section>

      <section>
        <p>{t("settings.appearance.fontSize")}</p>
        <p className="text-[11px] opacity-60">
          {t("settings.appearance.fontSizeHint")}
        </p>
      </section>
    </div>
  );
}
