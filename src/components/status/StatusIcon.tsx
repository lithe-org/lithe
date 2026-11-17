import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

interface StatusIconProps {
  icon: LucideIcon;
  label: string;
  testId: string;
  children?: ReactNode;
}

/** 右侧状态项统一外壳：固定 16px 图标、继承前景色、hover 降透明度。 */
export function StatusIcon({
  icon: Icon,
  label,
  testId,
  children,
}: StatusIconProps) {
  return (
    <span
      aria-label={label}
      className="flex h-[var(--lithe-icon-size)] shrink-0 items-center gap-0.5 text-[var(--lithe-bar-fg)] hover:opacity-70"
      data-testid={testId}
      title={label}
    >
      <Icon className="size-[var(--lithe-icon-size)]" />
      {children}
    </span>
  );
}
