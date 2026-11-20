import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { setVolume, toggleMute } from "../../../lib/api";
import { useSystemStore } from "../../../stores/system";
import { iconForVolume } from "../../status/VolumeIcon";

/** 拖动时用本地草稿即时反馈，系统事件回来后再让 store 接管。 */
export function VolumePanel() {
  const volume = useSystemStore((state) => state.volume);
  const { t } = useTranslation();
  const [draft, setDraft] = useState<number | null>(null);
  const level = draft ?? volume.level;
  const Icon = iconForVolume(level, volume.muted);

  useEffect(() => {
    setDraft(null);
  }, [volume.level]);

  const change = (value: number) => {
    setDraft(value);
    void setVolume(value).catch(() => {
      // 音量服务暂不可用时保留草稿，用户可再次拖动。
    });
  };

  return (
    <div data-testid="volume-panel">
      <div className="flex items-center gap-2">
        <Icon className="size-4 shrink-0" />
        <input
          aria-label={t("control.volume")}
          className="h-1 flex-1 accent-current"
          data-testid="volume-slider"
          max={100}
          min={0}
          onChange={(event) => change(Number(event.currentTarget.value))}
          step={1}
          type="range"
          value={level}
        />
        <span className="w-9 text-right text-[11px] tabular-nums">
          {level}%
        </span>
      </div>
      <button
        className="mt-2 rounded-[var(--lithe-radius)] px-2 py-1 text-[11px] hover:bg-black/10"
        data-testid="volume-mute"
        onClick={() => void toggleMute().catch(() => {})}
        type="button"
      >
        {volume.muted ? t("volumePanel.unmute") : t("volumePanel.mute")}
      </button>
    </div>
  );
}
