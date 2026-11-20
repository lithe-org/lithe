export type ClockLanguage = "zh-CN" | "en-US";

const pad = (value: number) => value.toString().padStart(2, "0");

/** 中文 YYYY/MM/DD HH:mm，英文 MM/DD/YYYY HH:mm；补零避免数字变宽抖动。 */
export function formatClock(date: Date, language: ClockLanguage = "zh-CN"): string {
  const year = date.getFullYear().toString();
  const month = pad(date.getMonth() + 1);
  const day = pad(date.getDate());
  const hour = pad(date.getHours());
  const minute = pad(date.getMinutes());

  return language === "en-US"
    ? `${month}/${day}/${year} ${hour}:${minute}`
    : `${year}/${month}/${day} ${hour}:${minute}`;
}

/**
 * 秒级 tick：只显示到分钟，分钟没变就不写 store，
 * 否则每秒一次 setState 会让顶栏整棵组件树重渲染 60 倍。
 * 语言切换后即使分钟没变也要写一次，否则格式会停在上一种语言。
 */
export function tickClock(
  previous: string,
  now: Date,
  write: (value: string) => void,
  language: ClockLanguage = "zh-CN",
): void {
  const formatted = formatClock(now, language);
  if (formatted !== previous) {
    write(formatted);
  }
}
