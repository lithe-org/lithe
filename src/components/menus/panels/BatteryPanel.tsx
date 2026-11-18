import { useSystemStore } from "../../../stores/system";

/** 台式机没有电池，按 PRD 5.4.3 显示简短提示而不是空白。 */
export function BatteryPanel() {
  const battery = useSystemStore((state) => state.battery);

  if (!battery.present) {
    return (
      <p className="text-[11px] opacity-60" data-testid="battery-summary">
        无电池（台式机）
      </p>
    );
  }

  return (
    <div className="text-[11px]" data-testid="battery-panel">
      <p className="text-[13px] tabular-nums" data-testid="battery-summary">
        {battery.percent}%
      </p>
      <p className="opacity-70" data-testid="battery-state">
        {battery.charging ? "正在充电 / 外接电源" : "使用电池供电"}
      </p>
    </div>
  );
}
