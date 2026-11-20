import { useTranslation } from "react-i18next";
import { applySplit, hidePopup } from "../../lib/api";
import type { SplitLayout } from "../../lib/api";

interface LayoutSpec {
  id: SplitLayout;
  key: string;
  /** 2×2 网格中高亮的格子：0 左上、1 右上、2 左下、3 右下。 */
  cells: number[];
}

// PRD 5.3.3 的六种布局，顺序按左/右/左上/右上/左下/右下排列。
const LAYOUTS: LayoutSpec[] = [
  { id: "left", key: "left", cells: [0, 2] },
  { id: "right", key: "right", cells: [1, 3] },
  { id: "top-left", key: "topLeft", cells: [0] },
  { id: "top-right", key: "topRight", cells: [1] },
  { id: "bottom-left", key: "bottomLeft", cells: [2] },
  { id: "bottom-right", key: "bottomRight", cells: [3] },
];

async function activate(layout: SplitLayout) {
  // 先收起面板，分屏失败也不会挡住用户视线。
  void hidePopup();
  try {
    await applySplit(layout);
  } catch {
    // 没有可控窗口时静默失败，避免弹系统对话框。
  }
}

export function SplitMenu() {
  const { t } = useTranslation();

  return (
    <div className="grid grid-cols-3 gap-1 p-2" data-testid="split-menu">
      {LAYOUTS.map((layout) => (
        <button
          aria-label={t(`split.${layout.key}`)}
          className="flex flex-col items-center gap-1 rounded-[var(--lithe-radius)] p-2 hover:bg-black/10"
          data-testid={`split-layout-${layout.id}`}
          key={layout.id}
          onClick={() => void activate(layout.id)}
          type="button"
        >
          <span className="grid size-6 grid-cols-2 grid-rows-2 gap-0.5">
            {[0, 1, 2, 3].map((cell) => (
              <span
                className={
                  layout.cells.includes(cell)
                    ? "rounded-[1px] bg-current"
                    : "rounded-[1px] border border-current/40"
                }
                data-active={layout.cells.includes(cell)}
                key={cell}
              />
            ))}
          </span>
          <span className="text-[11px] leading-none">
            {t(`split.${layout.key}`)}
          </span>
        </button>
      ))}
    </div>
  );
}
