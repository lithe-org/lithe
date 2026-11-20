import { existsSync, readFileSync, statSync } from "node:fs";
import { describe, expect, it } from "vitest";

const FONT_DIR = "src/assets/fonts";
const css = readFileSync("src/styles/index.css", "utf8");

const FACES: [string, string, number][] = [
  ["Inter", "inter-regular.woff2", 400],
  ["Inter", "inter-medium.woff2", 500],
  ["Inter", "inter-semibold.woff2", 600],
  ["Source Han Sans SC", "source-han-sans-sc-regular-subset.woff2", 400],
  ["Source Han Sans SC", "source-han-sans-sc-medium-subset.woff2", 500],
];

describe("字体打包", () => {
  it.each(FACES)("%s 的 %s 是有效 woff2 文件", (_family, file) => {
    const path = `${FONT_DIR}/${file}`;
    expect(existsSync(path), `${path} 不存在`).toBe(true);
    expect(readFileSync(path).subarray(0, 4).toString("ascii")).toBe("wOF2");
  });

  it("每个字体都声明了 @font-face、路径与字重", () => {
    for (const [family, file, weight] of FACES) {
      expect(css).toContain(`font-family: "${family}"`);
      expect(css).toContain(`url("../assets/fonts/${file}")`);
      expect(css).toContain(`font-weight: ${weight}`);
    }
    expect(css).toContain("font-display: swap");
  });

  it("中文字体子集体积可控，单个文件不超过 8MB", () => {
    for (const [, file] of FACES.filter(([family]) => family.startsWith("Source"))) {
      expect(statSync(`${FONT_DIR}/${file}`).size).toBeLessThan(8 * 1024 * 1024);
    }
  });

  it("字体栈先拉丁后中文，保证混排逐字回退", () => {
    expect(css).toContain(
      'font-family: "Inter", "Source Han Sans SC", system-ui, sans-serif',
    );
  });
});
