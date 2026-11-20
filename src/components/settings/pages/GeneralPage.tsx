import { disable, enable, isEnabled } from "@tauri-apps/plugin-autostart";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import i18n, { setLanguage } from "../../../lib/i18n";
import type { Language } from "../../../lib/i18n";

const LANGUAGES: { id: Language; key: string }[] = [
  { id: "zh-CN", key: "langZh" },
  { id: "en-US", key: "langEn" },
];

export function GeneralPage() {
  const { t } = useTranslation();
  const language: Language = i18n.language === "en-US" ? "en-US" : "zh-CN";
  const [autostart, setAutostart] = useState(false);

  useEffect(() => {
    let cancelled = false;
    isEnabled()
      .then((value) => {
        if (!cancelled) setAutostart(value);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  const toggleAutostart = async (next: boolean) => {
    setAutostart(next);
    try {
      if (next) {
        await enable();
      } else {
        await disable();
      }
    } catch {
      // 系统策略拒绝时回滚开关，避免界面与实际状态不一致。
      setAutostart(!next);
    }
  };

  return (
    <div className="flex flex-col gap-5 text-[13px]" data-testid="general-page">
      <section>
        <label className="flex items-center gap-2">
          <input
            checked={autostart}
            data-testid="startup-toggle"
            onChange={(event) =>
              void toggleAutostart(event.currentTarget.checked)
            }
            type="checkbox"
          />
          <span>{t("settings.general.startup")}</span>
        </label>
        <p className="mt-1 text-[11px] opacity-60">
          {t("settings.general.startupHint")}
        </p>
      </section>

      <section>
        <p className="mb-2">{t("settings.general.language")}</p>
        <div aria-label={t("settings.general.language")} className="flex gap-2" role="group">
          {LANGUAGES.map(({ id, key }) => (
            <button
              aria-pressed={language === id}
              className="rounded-[var(--lithe-radius)] border border-black/10 px-3 py-1 aria-pressed:border-[var(--lithe-accent)] aria-pressed:text-[var(--lithe-accent)]"
              data-testid={`language-${id}`}
              key={id}
              onClick={() => void setLanguage(id)}
              type="button"
            >
              {t(`settings.general.${key}`)}
            </button>
          ))}
        </div>
      </section>

      <section>
        <button
          className="rounded-[var(--lithe-radius)] border border-black/10 px-3 py-1 disabled:opacity-50"
          data-testid="check-updates"
          disabled
          type="button"
        >
          {t("settings.general.checkUpdate")}
        </button>
        <p className="mt-1 text-[11px] opacity-60">
          {t("settings.general.updateHint")}
        </p>
      </section>
    </div>
  );
}
