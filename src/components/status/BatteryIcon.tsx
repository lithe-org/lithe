import { Battery } from "lucide-react";
import { StatusIcon } from "./StatusIcon";

/** M3-08 接入电量与充电状态；台式机由数据采集侧决定隐藏。 */
export function BatteryIcon() {
  return <StatusIcon icon={Battery} label="电池" testId="status-battery" />;
}
