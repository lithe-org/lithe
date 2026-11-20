import { invoke } from "@tauri-apps/api/core";
import { useTranslation } from "react-i18next";
import { hidePopup } from "../../lib/api";

interface MenuEntry {
  id: string;
  /** i18n key 的末段，完整路径为 menu.<key>。 */
  key: string;
  separatorBefore?: boolean;
  disabled?: boolean;
  command?: string;
}

// PRD 5.2.1 的顺序与分隔线位置，改动需同步 PRD。
const ENTRIES: MenuEntry[] = [
  { id: "system-info", key: "systemInfo", command: "open_system_info" },
  { id: "windows-settings", key: "windowsSettings", command: "open_windows_settings" },
  {
    id: "lock",
    key: "lock",
    separatorBefore: true,
    command: "lock_workstation",
  },
  { id: "sleep", key: "sleep", command: "sleep" },
  { id: "restart", key: "restart", command: "restart" },
  { id: "shutdown", key: "shutdown", command: "shutdown" },
  {
    id: "task-manager",
    key: "taskManager",
    separatorBefore: true,
    command: "open_task_manager",
  },
  // 设置与关于窗口由 M6 提供，先占位禁用，避免点了没有任何反应。
  {
    id: "lithe-settings",
    key: "litheSettings",
    separatorBefore: true,
    disabled: true,
  },
  { id: "lithe-about", key: "litheAbout", disabled: true },
];

async function activate(entry: MenuEntry) {
  if (entry.disabled) {
    return;
  }
  // 先收起菜单，动作失败也不会留下悬空面板。
  void hidePopup();
  if (!entry.command) {
    return;
  }
  try {
    await invoke(entry.command);
  } catch {
    // 系统拒绝执行（如策略限制）时静默失败，不弹对话框打断用户。
  }
}

export function LogoMenu() {
  const { t } = useTranslation();

  return (
    <ul className="flex flex-col p-1" data-testid="logo-menu" role="menu">
      {ENTRIES.map((entry) => (
        <li key={entry.id} role="none">
          {entry.separatorBefore ? (
            <hr className="my-1 border-black/10" data-testid="menu-separator" />
          ) : null}
          <button
            className="flex h-8 w-full items-center rounded-[var(--lithe-radius)] px-3 text-left hover:bg-black/10 disabled:cursor-default disabled:opacity-40"
            data-testid={`menu-item-${entry.id}`}
            disabled={entry.disabled}
            onClick={() => void activate(entry)}
            role="menuitem"
            type="button"
          >
            {t(`menu.${entry.key}`)}
          </button>
        </li>
      ))}
    </ul>
  );
}
