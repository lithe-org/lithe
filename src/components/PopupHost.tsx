/** 下拉窗口的宿主容器；M2-04 起按事件注入 Logo 菜单等面板内容。 */
export function PopupHost() {
  return (
    <div
      className="h-full w-full bg-[var(--lithe-bar-tint)]"
      data-testid="popup-host"
    />
  );
}
