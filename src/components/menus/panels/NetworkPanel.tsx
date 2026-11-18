import { Lock, Wifi } from "lucide-react";
import { useEffect, useState } from "react";
import { listWifiNetworks } from "../../../lib/api";
import type { WifiNetwork } from "../../../lib/api";
import { useSystemStore } from "../../../stores/system";

type ScanState = "loading" | "ready" | "error";

/** M3-11 先只展示，连接动作留到后续版本（PRD 允许第一版仅展示）。 */
export function NetworkPanel() {
  const wifi = useSystemStore((state) => state.wifi);
  const [networks, setNetworks] = useState<WifiNetwork[]>([]);
  const [scanState, setScanState] = useState<ScanState>("loading");

  useEffect(() => {
    let cancelled = false;
    listWifiNetworks()
      .then((items) => {
        if (!cancelled) {
          // 异常返回值按空列表处理，避免整块面板因一次 IPC 抖动崩掉。
          setNetworks(Array.isArray(items) ? items : []);
          setScanState("ready");
        }
      })
      .catch(() => {
        if (!cancelled) {
          setScanState("error");
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div data-testid="network-panel">
      <p className="mb-2 text-[11px] opacity-70" data-testid="network-summary">
        {wifi.connected
          ? `已连接 ${wifi.ssid}（信号 ${wifi.signal}%）`
          : wifi.available
            ? "未连接"
            : "无 Wi-Fi 适配器"}
      </p>

      {scanState === "loading" ? (
        <p className="text-[11px] opacity-60">扫描中…</p>
      ) : null}
      {scanState === "error" ? (
        <p className="text-[11px] opacity-60">无法获取网络列表</p>
      ) : null}
      {scanState === "ready" && networks.length === 0 ? (
        <p className="text-[11px] opacity-60">未发现可用网络</p>
      ) : null}

      {scanState === "ready" && networks.length > 0 ? (
        <ul className="flex flex-col gap-0.5">
          {networks.map((network) => (
            <li
              className="flex items-center gap-2 rounded-[var(--lithe-radius)] px-2 py-1 hover:bg-black/5"
              data-testid={`wifi-network-${network.ssid}`}
              key={network.ssid}
            >
              <Wifi className="size-3.5 shrink-0" />
              <span className="min-w-0 flex-1 truncate">{network.ssid}</span>
              {network.secure ? (
                <Lock aria-label="加密网络" className="size-3 shrink-0 opacity-60" />
              ) : null}
              <span className="tabular-nums text-[11px] opacity-60">
                {network.signal}%
              </span>
              {network.connected ? (
                <span className="text-[10px] opacity-80">已连接</span>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
