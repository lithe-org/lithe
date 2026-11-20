use std::ffi::c_void;
use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::Graphics::Dwm::{
    DwmSetWindowAttribute, DWM_SYSTEMBACKDROP_TYPE, DWMWA_SYSTEMBACKDROP_TYPE,
    DWMSBT_TRANSIENTWINDOW,
};

/// DWMWA_SYSTEMBACKDROP_TYPE：3 = Acrylic（TransientWindow），2 = Mica（MainWindow）。
/// 状态栏是透明浮层，用 Acrylic；这里不改 WS_EX_NOACTIVATE，失焦不会变灰。
pub fn backdrop_type() -> DWM_SYSTEMBACKDROP_TYPE {
    DWMSBT_TRANSIENTWINDOW
}

pub fn apply_acrylic_backdrop(hwnd: HWND) -> bool {
    let value = backdrop_type();
    unsafe {
        DwmSetWindowAttribute(
            hwnd,
            DWMWA_SYSTEMBACKDROP_TYPE as u32,
            &value as *const DWM_SYSTEMBACKDROP_TYPE as *const c_void,
            std::mem::size_of::<DWM_SYSTEMBACKDROP_TYPE>() as u32,
        ) == 0
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn status_bar_uses_acrylic_not_mica() {
        // Mica 只给主窗口，透明浮层要 Acrylic；改错会表现为失焦变灰。
        assert_eq!(backdrop_type(), 3);
    }
}
