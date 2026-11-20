import { useTranslation } from "react-i18next";

/** M5-09 填充主题与强调色设置。 */
export function AppearancePage() {
  const { t } = useTranslation();
  return (
    <p data-testid="appearance-page">{t("settings.appearance.fontSizeHint")}</p>
  );
}
