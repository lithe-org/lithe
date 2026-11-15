import { LayoutGrid, Maximize, Minus, Pin } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useState } from "react";
import { getForegroundApp } from "../lib/api";

interface ControlSpec {
  id: string;
  label: string;
  Icon: LucideIcon;
}

// PRD 5.3.1 的四个控件；点击行为由 M2-07 的窗口控制命令接管。
const CONTROLS: ControlSpec[] = [
  { id: "pin", label: "置顶", Icon: Pin },
  { id: "maximize", label: "最大化", Icon: Maximize },
  { id: "minimize", label: "最小化", Icon: Minus },
  { id: "split", label: "分屏", Icon: LayoutGrid },
];

export function WindowControls() {
  const [target, setTarget] = useState("");

  // 只在悬停时查一次前台应用名，避免状态栏常驻轮询。
  const refreshTarget = async () => {
    try {
      const app = await getForegroundApp();
      setTarget(app.name);
    } catch {
      setTarget("");
    }
  };

  return (
    <div
      className="flex items-center gap-[var(--lithe-icon-gap)]"
      data-testid="window-controls"
      onMouseEnter={() => void refreshTarget()}
    >
      {CONTROLS.map(({ id, label, Icon }) => (
        <button
          aria-label={label}
          className="flex size-6 items-center justify-center rounded-[var(--lithe-radius)] hover:bg-black/10"
          data-testid={`window-control-${id}`}
          key={id}
          title={target ? `${label}：${target}` : label}
          type="button"
        >
          <Icon className="size-[var(--lithe-icon-size)]" />
        </button>
      ))}
    </div>
  );
}
