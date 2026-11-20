import { describe, expect, it, vi } from "vitest";
import { formatClock, tickClock } from "./clock";

describe("顶栏时间", () => {
  it("中文按 YYYY/MM/DD HH:mm 补零", () => {
    expect(formatClock(new Date(2026, 10, 15, 9, 5), "zh-CN")).toBe(
      "2026/11/15 09:05",
    );
    expect(formatClock(new Date(2026, 10, 15, 23, 59), "zh-CN")).toBe(
      "2026/11/15 23:59",
    );
    expect(formatClock(new Date(2026, 10, 15, 0, 0), "zh-CN")).toBe(
      "2026/11/15 00:00",
    );
  });

  it("英文按 MM/DD/YYYY HH:mm", () => {
    expect(formatClock(new Date(2026, 10, 15, 9, 5), "en-US")).toBe(
      "11/15/2026 09:05",
    );
    expect(formatClock(new Date(2026, 0, 2, 23, 59), "en-US")).toBe(
      "01/02/2026 23:59",
    );
  });

  it("同一分钟内不写 store，只有分钟变化才写一次", () => {
    const write = vi.fn();
    const now = new Date(2026, 10, 15, 9, 5, 42);

    tickClock("2026/11/15 09:05", now, write);
    expect(write).not.toHaveBeenCalled();

    tickClock(
      "2026/11/15 09:05",
      new Date(2026, 10, 15, 9, 6, 0),
      write,
    );
    expect(write).toHaveBeenCalledTimes(1);
    expect(write).toHaveBeenCalledWith("2026/11/15 09:06");
  });

  it("语言切换后即使分钟没变也刷新格式", () => {
    const write = vi.fn();

    tickClock(
      "2026/11/15 09:05",
      new Date(2026, 10, 15, 9, 5, 42),
      write,
      "en-US",
    );

    expect(write).toHaveBeenCalledWith("11/15/2026 09:05");
  });
});
