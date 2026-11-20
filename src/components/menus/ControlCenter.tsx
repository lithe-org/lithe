import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { resizePopup } from "../../lib/api";
import type { ReactElement } from "react";
import type { ControlModule } from "../../lib/panels";
import { BatteryPanel } from "./panels/BatteryPanel";
import { NetworkPanel } from "./panels/NetworkPanel";
import { VolumePanel } from "./panels/VolumePanel";
import { ThemePanel } from "./panels/ThemePanel";

// 顺序即 PRD 5.4.3 的模块顺序，标题走 i18n 的 control.<id>。
const MODULES: ControlModule[] = [
  "network",
  "volume",
  "battery",
  "resource",
  "theme",
];

/** 已实现的模块渲染真实内容，其余仍显示占位说明。 */
const PANELS: Partial<Record<ControlModule, () => ReactElement>> = {
  network: NetworkPanel,
  volume: VolumePanel,
  battery: BatteryPanel,
  theme: ThemePanel,
};

interface ControlCenterProps {
  focus?: ControlModule;
}

/** PRD 5.4.3 的控制中心骨架：模块位置先定死，内容逐期填充。 */
export function ControlCenter({ focus }: ControlCenterProps) {
  const viewportRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const { t } = useTranslation();

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
        {MODULES.map((id) => {
          const Panel = PANELS[id];
          return (
            <section
              className="mb-3 rounded-[var(--lithe-radius)] bg-black/5 p-3"
              data-module={id}
              data-testid={`control-module-${id}`}
              key={id}
            >
              <h3 className="mb-1 text-[12px] font-medium">{t(`control.${id}`)}</h3>
              {Panel ? (
                <Panel />
              ) : (
                <p className="text-[11px] opacity-60">
                  {t("control.resourcePlaceholder")}
                </p>
              )}
            </section>
          );
        })}
      </div>
    </div>
  );
}
