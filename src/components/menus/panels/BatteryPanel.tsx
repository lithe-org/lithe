import { useTranslation } from "react-i18next";
import { useSystemStore } from "../../../stores/system";

/** 台式机没有电池，按 PRD 5.4.3 显示简短提示而不是空白。 */
export function BatteryPanel() {
  const battery = useSystemStore((state) => state.battery);
  const { t } = useTranslation();

  if (!battery.present) {
    return (
      <p className="text-[11px] opacity-60" data-testid="battery-summary">
        {t("batteryPanel.noBattery")}
      </p>
    );
  }

  return (
    <div className="text-[11px]" data-testid="battery-panel">
      <p className="text-[13px] tabular-nums" data-testid="battery-summary">
        {battery.percent}%
      </p>
      <p className="opacity-70" data-testid="battery-state">
        {battery.charging
          ? t("batteryPanel.charging")
          : t("batteryPanel.discharging")}
      </p>
    </div>
  );
}
