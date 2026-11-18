import { openNotifications } from "../../lib/notifications";

interface ClockDisplayProps {
  time: string;
}

/** 只显示到分钟，格式由 lib/clock.ts 保证；点击交给原生通知/日历面板。 */
export function ClockDisplay({ time }: ClockDisplayProps) {
  return (
    <button
      className="tabular-nums hover:opacity-70"
      data-testid="clock"
      onClick={() => void openNotifications()}
      type="button"
    >
      {time || "--:--"}
    </button>
  );
}
