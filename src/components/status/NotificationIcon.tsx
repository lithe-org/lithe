import { Bell } from "lucide-react";
import { openNotifications } from "../../lib/notifications";
import { StatusIcon } from "./StatusIcon";

/** 原生通知中心入口（PRD 5.4.2：只做后门，不自绘通知列表）。 */
export function NotificationIcon() {
  return (
    <StatusIcon
      icon={Bell}
      label="通知"
      onClick={() => void openNotifications()}
      testId="status-notification"
    />
  );
}
