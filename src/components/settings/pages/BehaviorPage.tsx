import { useTranslation } from "react-i18next";
import { useSettingsStore } from "../../../stores/settings";

export function BehaviorPage() {
  const { t } = useTranslation();
  const autoHide = useSettingsStore((state) => state.autoHideFullscreen);
  const doubleClick = useSettingsStore((state) => state.doubleClickOnEmpty);
  const setAutoHide = useSettingsStore((state) => state.setAutoHideFullscreen);
  const setDoubleClick = useSettingsStore(
    (state) => state.setDoubleClickOnEmpty,
  );

  return (
    <div className="flex flex-col gap-4 text-[13px]" data-testid="behavior-page">
      <label className="flex items-center gap-2">
        <input
          checked={autoHide}
          data-testid="behavior-autohide"
          onChange={(event) => setAutoHide(event.currentTarget.checked)}
          type="checkbox"
        />
        <span>{t("settings.behavior.autoHide")}</span>
      </label>
      <label className="flex items-center gap-2">
        <input
          checked={doubleClick}
          data-testid="behavior-double-click"
          onChange={(event) => setDoubleClick(event.currentTarget.checked)}
          type="checkbox"
        />
        <span>{t("settings.behavior.doubleClick")}</span>
      </label>
    </div>
  );
}
