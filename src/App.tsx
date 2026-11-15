function App() {
  // 顶栏骨架先留空容器：M1-07 起替换为三区窗口布局，避免模板 UI 混进后续视觉基线。
  return (
    <div
      className="lithe-root flex h-[var(--lithe-bar-height)] w-full items-center px-[var(--lithe-bar-padding-x)]"
      data-testid="lithe-root"
    />
  );
}

export default App;
