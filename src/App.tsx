import { useEffect } from "react";
import { getForegroundApp } from "./lib/api";
import { useSystemStore } from "./stores/system";
import { useUiStore } from "./stores/ui";

function App() {
  // 占位文本只用于验证 store 订阅与前后端链路；M1-08 起替换为三区布局。
  const foregroundApp = useSystemStore((state) => state.foregroundApp);
  const setForegroundApp = useSystemStore((state) => state.setForegroundApp);
  const language = useUiStore((state) => state.language);

  useEffect(() => {
    let cancelled = false;
    getForegroundApp()
      .then((app) => {
        if (!cancelled) setForegroundApp(app.name);
      })
      .catch(() => {
        // IPC 不可用时保留占位文案，不让顶栏启动被一次查询打断。
      });
    return () => {
      cancelled = true;
    };
  }, [setForegroundApp]);

  return (
    <div
      className="lithe-root flex h-[var(--lithe-bar-height)] w-full items-center gap-[var(--lithe-icon-gap)] px-[var(--lithe-bar-padding-x)]"
      data-testid="lithe-root"
    >
      <span data-testid="foreground-app">{foregroundApp}</span>
      <span data-testid="language">{language}</span>
    </div>
  );
}

export default App;
