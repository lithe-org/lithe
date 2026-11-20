import { useTranslation } from "react-i18next";
import type { VisibilitySettings } from "../../../stores/settings";
import {
  BAR_HEIGHT_MAX,
  BAR_HEIGHT_MIN,
  useSettingsStore,
} from "../../../stores/settings";

const ITEMS: { key: keyof VisibilitySettings }[] = [
  { key: "resource" },
  { key: "wifi" },
  { key: "volume" },
  { key: "battery" },
  { key: "datetime" },
];

export function StatusBarPage() {
  const { t } = useTranslation();
  const visibility = useSettingsStore((state) => state.visibility);
  const barHeight = useSettingsStore((state) => state.barHeight);
  const toggleVisibility = useSettingsStore((state) => state.toggleVisibility);
  const setBarHeight = useSettingsStore((state) => state.setBarHeight);

  return (
    <div className="flex flex-col gap-5 text-[13px]" data-testid="statusbar-page">
      <section>
        <p className="mb-2">{t("settings.statusBar.visibility")}</p>
        <div className="flex flex-col gap-2">
          {ITEMS.map(({ key }) => (
            <label className="flex items-center gap-2" key={key}>
              <input
                checked={visibility[key]}
                data-testid={`visibility-${key}`}
                onChange={() => toggleVisibility(key)}
                type="checkbox"
              />
              <span>{t(`settings.statusBar.items.${key}`)}</span>
            </label>
          ))}
        </div>
      </section>

      <section>
        <label className="flex items-center gap-3">
          <span>{t("settings.statusBar.height")}</span>
          <input
            aria-label={t("settings.statusBar.height")}
            className="w-48"
            data-testid="bar-height"
            max={BAR_HEIGHT_MAX}
            min={BAR_HEIGHT_MIN}
            onChange={(event) => setBarHeight(Number(event.currentTarget.value))}
            step={1}
            type="range"
            value={barHeight}
          />
          <span className="tabular-nums" data-testid="bar-height-value">
            {barHeight}px
          </span>
        </label>
      </section>
    </div>
  );
}
