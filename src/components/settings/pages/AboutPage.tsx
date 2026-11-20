import { getName, getVersion } from "@tauri-apps/api/app";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

const REPO_URL = "https://github.com/lithe-org/lithe";

/** 名称与版本从 Tauri 清单读取，避免和 tauri.conf.json 漂移。 */
export function AboutPage() {
  const { t } = useTranslation();
  const [name, setName] = useState("Lithe");
  const [version, setVersion] = useState("0.1.0");

  useEffect(() => {
    let cancelled = false;
    getName()
      .then((value) => {
        if (!cancelled) setName(value);
      })
      .catch(() => {});
    getVersion()
      .then((value) => {
        if (!cancelled) setVersion(value);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="flex flex-col gap-3 text-[13px]" data-testid="about-page">
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1">
        <dt className="opacity-60">{t("settings.about.appName")}</dt>
        <dd data-testid="about-name">{name}</dd>
        <dt className="opacity-60">{t("settings.about.version")}</dt>
        <dd data-testid="about-version">{version}</dd>
      </dl>

      <p>{t("settings.about.license")}</p>

      <div className="flex gap-4">
        <a
          className="text-[var(--lithe-accent)] underline"
          data-testid="about-repo"
          href={REPO_URL}
          rel="noreferrer"
          target="_blank"
        >
          {t("settings.about.repo")}
        </a>
        <a
          className="text-[var(--lithe-accent)] underline"
          data-testid="about-issues"
          href={`${REPO_URL}/issues`}
          rel="noreferrer"
          target="_blank"
        >
          {t("settings.about.issues")}
        </a>
      </div>

      <section>
        <p className="mb-1">{t("settings.about.credits")}</p>
        <p className="opacity-70">{t("settings.about.creditsText")}</p>
      </section>
    </div>
  );
}
