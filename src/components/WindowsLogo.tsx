interface WindowsLogoProps {
  className?: string;
}

/** Windows 11 四格 logo：用 currentColor 跟随前景色，尺寸取设计 token。 */
export function WindowsLogo({ className }: WindowsLogoProps) {
  return (
    <svg
      aria-hidden="true"
      className={`size-[var(--lithe-icon-size)] shrink-0 ${className ?? ""}`}
      data-testid="windows-logo"
      viewBox="0 0 16 16"
    >
      <rect x="1" y="1" width="6.2" height="6.2" rx="0.5" fill="currentColor" />
      <rect x="8.8" y="1" width="6.2" height="6.2" rx="0.5" fill="currentColor" />
      <rect x="1" y="8.8" width="6.2" height="6.2" rx="0.5" fill="currentColor" />
      <rect x="8.8" y="8.8" width="6.2" height="6.2" rx="0.5" fill="currentColor" />
    </svg>
  );
}
