import { useTranslation } from "react-i18next";

/** M5-12 填充版本、协议与链接。 */
export function AboutPage() {
  const { t } = useTranslation();
  return <p data-testid="about-page">{t("settings.about.license")}</p>;
}
