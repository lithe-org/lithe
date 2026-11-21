import windows11Icon from "../assets/windows-11.svg";

interface WindowsLogoProps {
  className?: string;
}

/** 使用用户提供的 Windows 11 四格图标资源，尺寸取设计 token。 */
export function WindowsLogo({ className }: WindowsLogoProps) {
  return (
    <img
      alt=""
      aria-hidden="true"
      className={`size-[var(--lithe-icon-size)] shrink-0 ${className ?? ""}`}
      data-testid="windows-logo"
      src={windows11Icon}
    />
  );
}
