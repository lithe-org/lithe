import { Wifi, WifiOff } from "lucide-react";
import { useSystemStore } from "../../stores/system";
import { StatusIcon } from "./StatusIcon";

/** PRD 5.4.2：无适配器隐藏；连接方式决定图标，信号强度用透明度表达。 */
export function WifiIcon() {
  const wifi = useSystemStore((state) => state.wifi);

  if (!wifi.available) {
    return null;
  }

  const opacity = wifi.connected ? Math.max(0.45, wifi.signal / 100) : 1;
  const label = wifi.connected
    ? `Wi-Fi：${wifi.ssid}（信号 ${wifi.signal}%）`
    : "Wi-Fi 未连接";

  return (
    <span className="flex items-center" style={{ opacity }}>
      <StatusIcon
        icon={wifi.connected ? Wifi : WifiOff}
        label={label}
        testId="status-wifi"
      />
    </span>
  );
}
