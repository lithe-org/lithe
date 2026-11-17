import { Volume2 } from "lucide-react";
import { StatusIcon } from "./StatusIcon";

/** M3-07 接入音量与静音状态。 */
export function VolumeIcon() {
  return <StatusIcon icon={Volume2} label="音量" testId="status-volume" />;
}
