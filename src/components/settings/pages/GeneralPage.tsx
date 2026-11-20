import { useTranslation } from "react-i18next";

/** M5-08 填充具体设置项。 */
export function GeneralPage() {
  const { t } = useTranslation();
  return <p data-testid="general-page">{t("settings.general.updateHint")}</p>;
}
