import { useEffect, useRef } from "react";
import { resizePopup } from "../../lib/api";
import type { ReactElement } from "react";
import type { ControlModule } from "../../lib/panels";
import { BatteryPanel } from "./panels/BatteryPanel";
import { NetworkPanel } from "./panels/NetworkPanel";
import { VolumePanel } from "./panels/VolumePanel";

const MODULES: { id: ControlModule; title: string; placeholder: string }[] = [
  { id: "network", title: "网络", placeholder: "Wi-Fi 连接与信号（M3-11 填充）" },
  { id: "volume", title: "音量", placeholder: "音量滑块与静音开关（M3-12 填充）" },
  { id: "battery", title: "电池", placeholder: "电量与充电状态（M3-12 填充）" },
  { id: "resource", title: "资源", placeholder: "CPU 与内存占用（后续填充）" },
  { id: "theme", title: "主题", placeholder: "亮色 / 暗色 / 跟随系统（M5 填充）" },
];

/** 已实现的模块渲染真实内容，其余仍显示占位说明。 */
const PANELS: Partial<Record<ControlModule, () => ReactElement>> = {
  network: NetworkPanel,
  volume: VolumePanel,
  battery: BatteryPanel,
};

interface ControlCenterProps {
  focus?: ControlModule;
}

/** PRD 5.4.3 的控制中心骨架：模块位置先定死，内容逐期填充。 */
export function ControlCenter({ focus }: ControlCenterProps) {
  const viewportRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // 宽度由窗口池固定，这里只把高度按内容收缩，并留一个屏幕内的上限。
    const height = Math.min(
      720,
      Math.max(120, Math.ceil(contentRef.current?.scrollHeight ?? 400)),
    );
    void resizePopup(height);
  }, []);

  useEffect(() => {
    if (!focus) {
      return;
    }
    const target = viewportRef.current?.querySelector(
      `[data-module="${focus}"]`,
    );
    target?.scrollIntoView?.({ block: "start" });
  }, [focus]);

  return (
    <div
      className="h-full w-full overflow-y-auto"
      data-testid="control-center"
      ref={viewportRef}
    >
      <div className="p-3" ref={contentRef}>
        {MODULES.map((module) => {
          const Panel = PANELS[module.id];
          return (
            <section
              className="mb-3 rounded-[var(--lithe-radius)] bg-black/5 p-3"
              data-module={module.id}
              data-testid={`control-module-${module.id}`}
              key={module.id}
            >
              <h3 className="mb-1 text-[12px] font-medium">{module.title}</h3>
              {Panel ? (
                <Panel />
              ) : (
                <p className="text-[11px] opacity-60">{module.placeholder}</p>
              )}
            </section>
          );
        })}
      </div>
    </div>
  );
}
