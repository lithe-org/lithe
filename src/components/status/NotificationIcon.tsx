import { Bell } from "lucide-react";
import { StatusIcon } from "./StatusIcon";

/** 原生通知中心入口，M3-12 接入控制中心面板时一起处理点击。 */
export function NotificationIcon() {
  return <StatusIcon icon={Bell} label="通知" testId="status-notification" />;
}
