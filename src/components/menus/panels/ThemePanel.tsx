import { Monitor, Moon, RotateCcw, Sun } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useState } from "react";
import { ACCENT_PRESETS, useThemeStore } from "../../../stores/theme";
import type { ThemeMode } from "../../../stores/theme";

const MODES: { id: ThemeMode; label: string; icon: LucideIcon }[] = [
  { id: "light", label: "亮色", icon: Sun },
  { id: "dark", label: "暗色", icon: Moon },
  { id: "system", label: "跟随系统", icon: Monitor },
];

/** PRD 5.4.3 的主题模块：三档模式 + 预设色 + 自定义 hex。 */
export function ThemePanel() {
  const mode = useThemeStore((state) => state.mode);
  const accent = useThemeStore((state) => state.accent);
  const setMode = useThemeStore((state) => state.setMode);
  const setAccent = useThemeStore((state) => state.setAccent);
  const resetAccent = useThemeStore((state) => state.resetAccent);
  const [draft, setDraft] = useState(accent);

  return (
    <div className="text-[11px]" data-testid="theme-panel">
      <div aria-label="主题模式" className="flex gap-1" role="group">
        {MODES.map(({ id, label, icon: Icon }) => (
          <button
            aria-label={label}
            aria-pressed={mode === id}
            className="flex items-center gap-1 rounded-[var(--lithe-radius)] px-2 py-1 hover:bg-black/10 aria-pressed:bg-[var(--lithe-accent)] aria-pressed:text-white"
            data-testid={`theme-mode-${id}`}
            key={id}
            onClick={() => setMode(id)}
            type="button"
          >
            <Icon className="size-3" />
            {label}
          </button>
        ))}
      </div>

      <div aria-label="强调色" className="mt-2 flex items-center gap-1" role="group">
        {ACCENT_PRESETS.map((color) => (
          <button
            aria-label={`强调色 ${color}`}
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
          aria-label="自定义强调色"
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
          aria-label="恢复系统强调色"
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
        当前 {accent}
      </p>
    </div>
  );
}
