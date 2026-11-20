import { useState } from "react";
import type { ReactElement } from "react";
import { useTranslation } from "react-i18next";
import { AboutPage } from "./pages/AboutPage";
import { AppearancePage } from "./pages/AppearancePage";
import { BehaviorPage } from "./pages/BehaviorPage";
import { GeneralPage } from "./pages/GeneralPage";
import { StatusBarPage } from "./pages/StatusBarPage";
import { UpdatesPage } from "./pages/UpdatesPage";

export type SettingsCategory =
  | "general"
  | "appearance"
  | "statusBar"
  | "behavior"
  | "updates"
  | "about";

// PRD 5.5.1 的分类顺序，改动需同步 PRD 与测试。
const CATEGORIES: SettingsCategory[] = [
  "general",
  "appearance",
  "statusBar",
  "behavior",
  "updates",
  "about",
];

const PAGES: Record<SettingsCategory, () => ReactElement> = {
  general: GeneralPage,
  appearance: AppearancePage,
  statusBar: StatusBarPage,
  behavior: BehaviorPage,
  updates: UpdatesPage,
  about: AboutPage,
};

export function SettingsWindow() {
  const [active, setActive] = useState<SettingsCategory>("general");
  const { t } = useTranslation();
  const Page = PAGES[active];

  return (
    <div
      className="flex h-screen bg-white text-neutral-900 dark:bg-neutral-900 dark:text-neutral-100"
      data-testid="settings-window"
    >
      <nav
        aria-label={t("settings.title")}
        className="w-48 shrink-0 border-r border-black/10 p-2 dark:border-white/10"
      >
        {CATEGORIES.map((id) => (
          <button
            aria-current={active === id ? "page" : undefined}
            className="mb-1 w-full rounded-[var(--lithe-radius)] px-3 py-2 text-left text-[13px] hover:bg-black/5 aria-current:bg-[var(--lithe-accent)] aria-current:text-white"
            data-testid={`settings-nav-${id}`}
            key={id}
            onClick={() => setActive(id)}
            type="button"
          >
            {t(`settings.categories.${id}`)}
          </button>
        ))}
      </nav>
      <main className="flex-1 overflow-y-auto p-6" data-testid="settings-content">
        <h1 className="mb-4 text-lg font-medium">
          {t(`settings.categories.${active}`)}
        </h1>
        <Page />
      </main>
    </div>
  );
}
