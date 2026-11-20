import { useTranslation } from "react-i18next";

/** M5-11 填充全屏隐藏与双击行为开关。 */
export function BehaviorPage() {
  const { t } = useTranslation();
  return <p data-testid="behavior-page">{t("settings.behavior.autoHide")}</p>;
}
