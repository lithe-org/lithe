interface ClockDisplayProps {
  time: string;
}

/** 只显示到分钟，格式由 lib/clock.ts 保证。 */
export function ClockDisplay({ time }: ClockDisplayProps) {
  return (
    <time className="tabular-nums" data-testid="clock">
      {time || "--:--"}
    </time>
  );
}
