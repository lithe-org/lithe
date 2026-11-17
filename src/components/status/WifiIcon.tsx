import { Wifi } from "lucide-react";
import { StatusIcon } from "./StatusIcon";

/** M3-01 先占位；M3-05 按连接状态与信号强度切换图标。 */
export function WifiIcon() {
  return <StatusIcon icon={Wifi} label="Wi-Fi" testId="status-wifi" />;
}
