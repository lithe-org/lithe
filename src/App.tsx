import { useSystemStore } from "./stores/system";
import { useUiStore } from "./stores/ui";

function App() {
  // 占位文本只用于验证 store 订阅链路；M1-08 起替换为三区布局。
  const foregroundApp = useSystemStore((state) => state.foregroundApp);
  const language = useUiStore((state) => state.language);

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
