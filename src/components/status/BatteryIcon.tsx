import {
  BatteryCharging,
  BatteryFull,
  BatteryLow,
  BatteryMedium,
  BatteryWarning,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { openControlCenter } from "../../lib/panels";
import { useSystemStore } from "../../stores/system";
import { StatusIcon } from "./StatusIcon";

/** 充电状态优先于电量档位，低电量先警告再低/中/满。 */
export function iconForBattery(percent: number, charging: boolean): LucideIcon {
  if (charging) {
    return BatteryCharging;
  }
  if (percent <= 10) {
    return BatteryWarning;
  }
  if (percent <= 30) {
    return BatteryLow;
  }
  if (percent <= 70) {
    return BatteryMedium;
  }
  return BatteryFull;
}

export function BatteryIcon() {
  const battery = useSystemStore((state) => state.battery);
  const { t } = useTranslation();

  if (!battery.present) {
    return null;
  }

  const Icon = iconForBattery(battery.percent, battery.charging);

  return (
    <StatusIcon
      icon={Icon}
      label={
        battery.charging
          ? t("status.batteryCharging", { percent: battery.percent })
          : t("status.battery", { percent: battery.percent })
      }
      onClick={(event) => openControlCenter(event.currentTarget, "battery")}
      testId="status-battery"
    />
  );
}
