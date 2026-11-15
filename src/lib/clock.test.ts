import { describe, expect, it, vi } from "vitest";
import { formatClock, tickClock } from "./clock";

describe("顶栏时间", () => {
  it("按 HH:mm 补零，避免数字变宽时顶栏抖动", () => {
    expect(formatClock(new Date(2026, 10, 15, 9, 5))).toBe("09:05");
    expect(formatClock(new Date(2026, 10, 15, 23, 59))).toBe("23:59");
    expect(formatClock(new Date(2026, 10, 15, 0, 0))).toBe("00:00");
  });

  it("同一分钟内不写 store，只有分钟变化才写一次", () => {
    const write = vi.fn();

    tickClock("09:05", new Date(2026, 10, 15, 9, 5, 42), write);
    expect(write).not.toHaveBeenCalled();

    tickClock("09:05", new Date(2026, 10, 15, 9, 6, 0), write);
    expect(write).toHaveBeenCalledTimes(1);
    expect(write).toHaveBeenCalledWith("09:06");
  });
});
