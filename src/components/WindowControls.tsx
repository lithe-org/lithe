import { LayoutGrid, Maximize, Minus, Pin } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useState } from "react";
import {
  captureControlTarget,
  minimizeForeground,
  toggleMaximize,
  toggleTopmost,
} from "../lib/api";
import type { ForegroundApp } from "../lib/api";

interface ControlSpec {
  id: string;
  label: string;
  Icon: LucideIcon;
}

// PRD 5.3.1 的四个控件；分屏在 M2-09 接入面板，其余三项直接执行命令。
const CONTROLS: ControlSpec[] = [
  { id: "pin", label: "置顶", Icon: Pin },
  { id: "maximize", label: "最大化", Icon: Maximize },
  { id: "minimize", label: "最小化", Icon: Minus },
  { id: "split", label: "分屏", Icon: LayoutGrid },
];

const ACTIONS: Record<string, () => Promise<unknown>> = {
  pin: toggleTopmost,
  maximize: toggleMaximize,
  minimize: minimizeForeground,
};

export function WindowControls() {
  const [target, setTarget] = useState<ForegroundApp | null>(null);

  // 只在悬停与按下时查询，避免状态栏常驻轮询。
  const captureTarget = async () => {
    try {
      setTarget(await captureControlTarget());
    } catch {
      setTarget(null);
    }
  };

  return (
    <div
      className="flex items-center gap-[var(--lithe-icon-gap)]"
      data-testid="window-controls"
      onMouseEnter={() => void captureTarget()}
    >
      {CONTROLS.map(({ id, label, Icon }) => (
        <button
          aria-label={label}
          className="flex size-6 items-center justify-center rounded-[var(--lithe-radius)] hover:bg-black/10"
          data-testid={`window-control-${id}`}
          key={id}
          // 按下即记录目标：点击时前台窗口可能已经变化。
          onMouseDown={() => void captureTarget()}
          onClick={() => {
            const action = ACTIONS[id];
            if (action) {
              void action();
            }
          }}
          title={target ? `${label}：${target.name}` : label}
          type="button"
        >
          <Icon className="size-[var(--lithe-icon-size)]" />
        </button>
      ))}
    </div>
  );
}
