import { useTranslation } from "react-i18next";

/** M5-08 更新页先占位，自动更新在 M6 接入。 */
export function UpdatesPage() {
  const { t } = useTranslation();
  return <p data-testid="updates-page">{t("settings.updates.hint")}</p>;
}
