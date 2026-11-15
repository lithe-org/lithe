import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// CSS 走 Vite 样式管道后 ?raw 会得到空串；用工作区相对路径读源文件，
// 避免依赖 import.meta.url（Vitest 下未必是 file: scheme）。
const css = readFileSync("src/styles/index.css", "utf8");

// 尺寸类数值最容易在照抄 PRD 时静默偏离，把可度量的基线钉成断言。
const BASELINE = {
  "--lithe-bar-height": [28, 36],
  "--lithe-bar-padding-x": [12, 12],
  "--lithe-icon-size": [16, 16],
  "--lithe-icon-gap": [8, 10],
  "--lithe-font-size": [12, 13],
  "--lithe-radius": [6, 8],
} as const;

function readToken(name: string): number {
  const match = css.match(new RegExp(`${name}\\s*:\\s*(\\d+(?:\\.\\d+)?)px`));
  if (!match) {
    throw new Error(`设计 token ${name} 缺失或不是 px 数值`);
  }
  return Number(match[1]);
}

describe("设计 token 基线", () => {
  for (const [name, [min, max]] of Object.entries(BASELINE)) {
    it(`${name} 必须落在 PRD 6.5 规定区间内`, () => {
      const value = readToken(name);
      expect(value).toBeGreaterThanOrEqual(min);
      expect(value).toBeLessThanOrEqual(max);
    });
  }

  it("样式入口必须引入 Tailwind，否则工具类不会被生成", () => {
    expect(css).toContain('@import "tailwindcss"');
  });
});
