import { Volume, Volume1, Volume2, VolumeX } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useSystemStore } from "../../stores/system";
import { StatusIcon } from "./StatusIcon";

/** 静音与 0% 共用静音图标，其余按三档表达强度。 */
export function iconForVolume(level: number, muted: boolean): LucideIcon {
  if (muted || level <= 0) {
    return VolumeX;
  }
  if (level < 34) {
    return Volume;
  }
  if (level < 67) {
    return Volume1;
  }
  return Volume2;
}

export function VolumeIcon() {
  const volume = useSystemStore((state) => state.volume);
  const Icon = iconForVolume(volume.level, volume.muted);

  return (
    <StatusIcon
      icon={Icon}
      label={volume.muted ? "音量已静音" : `音量 ${volume.level}%`}
      testId="status-volume"
    />
  );
}
