/** 顶栏时间固定 HH:mm；按语言切换的日期格式在 M5 实现。 */
export function formatClock(date: Date): string {
  const hours = date.getHours().toString().padStart(2, "0");
  const minutes = date.getMinutes().toString().padStart(2, "0");
  return `${hours}:${minutes}`;
}

/**
 * 秒级 tick：只显示到分钟，分钟没变就不写 store，
 * 否则每秒一次 setState 会让顶栏整棵组件树重渲染 60 倍。
 */
export function tickClock(
  previous: string,
  now: Date,
  write: (value: string) => void,
): void {
  const formatted = formatClock(now);
  if (formatted !== previous) {
    write(formatted);
  }
}
