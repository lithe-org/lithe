import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { AboutPage } from "./AboutPage";

afterEach(() => cleanup());

describe("关于页", () => {
  it("显示清单中的应用名称与版本", async () => {
    render(<AboutPage />);

    expect((await screen.findByTestId("about-name")).textContent).toBe("Lithe");
    expect(screen.getByTestId("about-version").textContent).toBe("0.1.0");
  });

  it("仓库与 Issue 链接可点击且地址正确", () => {
    render(<AboutPage />);

    const repo = screen.getByTestId("about-repo") as HTMLAnchorElement;
    expect(repo.href).toBe("https://github.com/lithe-org/lithe");
    expect(repo.target).toBe("_blank");
    expect(repo.rel).toBe("noreferrer");

    const issues = screen.getByTestId("about-issues") as HTMLAnchorElement;
    expect(issues.href).toBe("https://github.com/lithe-org/lithe/issues");
  });

  it("GPL 协议与致谢列表可见", () => {
    render(<AboutPage />);

    expect(screen.getByText("开源协议：GPL-3.0-only")).not.toBeNull();
    expect(screen.getByText(/Tauri/)).not.toBeNull();
  });
});
