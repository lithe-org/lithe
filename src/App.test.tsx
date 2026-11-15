import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import App from "./App";

describe("顶栏骨架", () => {
  it("清理模板后只渲染空白容器，不再出现示例文案与图标", () => {
    const { container } = render(<App />);

    expect(container.querySelector('[data-testid="lithe-root"]')).not.toBeNull();
    expect(screen.queryByText(/Welcome to Tauri/)).toBeNull();
    expect(container.querySelector("img")).toBeNull();
    expect(container.querySelector("form")).toBeNull();
  });
});
