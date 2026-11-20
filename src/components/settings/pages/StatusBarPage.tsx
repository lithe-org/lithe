import { useTranslation } from "react-i18next";

/** M5-10 填充状态项开关与高度调节。 */
export function StatusBarPage() {
  const { t } = useTranslation();
  return (
    <p data-testid="statusbar-page">{t("settings.statusBar.visibility")}</p>
  );
}
