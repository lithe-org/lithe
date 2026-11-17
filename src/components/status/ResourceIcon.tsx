import { Activity } from "lucide-react";
import { StatusIcon } from "./StatusIcon";

/** M3-01 先占位；M3-03 接入 CPU 与内存 percentage。 */
export function ResourceIcon() {
  return (
    <StatusIcon icon={Activity} label="资源监控" testId="status-resource">
      <span className="text-[11px] tabular-nums" data-testid="resource-values">
        0% 0%
      </span>
    </StatusIcon>
  );
}
