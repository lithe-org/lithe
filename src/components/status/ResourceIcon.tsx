import { Activity } from "lucide-react";
import { openControlCenter } from "../../lib/panels";
import { useTranslation } from "react-i18next";
import { useSystemStore } from "../../stores/system";
import { StatusIcon } from "./StatusIcon";

/** PRD 5.4.2：默认显示 CPU 与内存百分比，用户设置项开关留到 M5。 */
export function ResourceIcon() {
  const cpuPercent = useSystemStore((state) => state.cpuPercent);
  const memoryPercent = useSystemStore((state) => state.memoryPercent);
  const cpu = Math.round(cpuPercent);
  const memory = Math.round(memoryPercent);
  const { t } = useTranslation();

  return (
    <StatusIcon
      icon={Activity}
      label={t("status.cpuMemory", { cpu, memory })}
      onClick={(event) => openControlCenter(event.currentTarget, "resource")}
      testId="status-resource"
    >
      <span className="text-[11px] tabular-nums" data-testid="resource-values">
        {cpu}% {memory}%
      </span>
    </StatusIcon>
  );
}
