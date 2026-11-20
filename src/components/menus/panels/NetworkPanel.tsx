import { Lock, Wifi } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { listWifiNetworks } from "../../../lib/api";
import type { WifiNetwork } from "../../../lib/api";
import { useSystemStore } from "../../../stores/system";

type ScanState = "loading" | "ready" | "error";

/** M3-11 先只展示，连接动作留到后续版本（PRD 允许第一版仅展示）。 */
export function NetworkPanel() {
  const wifi = useSystemStore((state) => state.wifi);
  const [networks, setNetworks] = useState<WifiNetwork[]>([]);
  const [scanState, setScanState] = useState<ScanState>("loading");
  const { t } = useTranslation();

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
          ? t("network.connected", { ssid: wifi.ssid, signal: wifi.signal })
          : wifi.available
            ? t("network.notConnected")
            : t("network.noAdapter")}
      </p>

      {scanState === "loading" ? (
        <p className="text-[11px] opacity-60">{t("network.scanning")}</p>
      ) : null}
      {scanState === "error" ? (
        <p className="text-[11px] opacity-60">{t("network.failed")}</p>
      ) : null}
      {scanState === "ready" && networks.length === 0 ? (
        <p className="text-[11px] opacity-60">{t("network.empty")}</p>
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
                <Lock
                  aria-label={t("network.secured")}
                  className="size-3 shrink-0 opacity-60"
                />
              ) : null}
              <span className="tabular-nums text-[11px] opacity-60">
                {network.signal}%
              </span>
              {network.connected ? (
                <span className="text-[10px] opacity-80">
                  {t("network.connectedBadge")}
                </span>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
