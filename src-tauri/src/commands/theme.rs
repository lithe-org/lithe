use serde::Serialize;
use windows_sys::Win32::System::Registry::{
    RegCloseKey, RegOpenKeyExW, RegQueryValueExW, HKEY_CURRENT_USER, KEY_READ,
};

const PERSONALIZE_PATH: &str =
    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
const DWM_PATH: &str = "Software\\Microsoft\\Windows\\DWM";
const DEFAULT_ACCENT: &str = "#0078D4";

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ThemeInfo {
    pub dark: bool,
    /// CSS #RRGGBB
    pub accent: String,
}

/// DWM 的 AccentColor 是 ABGR（0xAABBGGRR），前端要的是 #RRGGBB。
pub fn accent_from_abgr(value: u32) -> String {
    let red = value & 0xFF;
    let green = (value >> 8) & 0xFF;
    let blue = (value >> 16) & 0xFF;
    format!("#{red:02X}{green:02X}{blue:02X}")
}

fn read_u32(path: &str, name: &str) -> Option<u32> {
    let path: Vec<u16> = path.encode_utf16().chain(std::iter::once(0)).collect();
    let name: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
    unsafe {
        let mut key = std::ptr::null_mut();
        if RegOpenKeyExW(HKEY_CURRENT_USER, path.as_ptr(), 0, KEY_READ, &mut key) != 0 {
            return None;
        }
        let mut value = 0u32;
        let mut size = std::mem::size_of::<u32>() as u32;
        let ok = RegQueryValueExW(
            key,
            name.as_ptr(),
            std::ptr::null(),
            std::ptr::null_mut(),
            &mut value as *mut u32 as *mut u8,
            &mut size,
        ) == 0;
        RegCloseKey(key);
        ok.then_some(value)
    }
}

/// 读当前系统主题：AppsUseLightTheme=0 为暗色，强调色来自 DWM。
pub fn get_system_theme() -> ThemeInfo {
    let dark = read_u32(PERSONALIZE_PATH, "AppsUseLightTheme")
        .map(|value| value == 0)
        .unwrap_or(false);
    let accent = read_u32(DWM_PATH, "AccentColor")
        .map(accent_from_abgr)
        .unwrap_or_else(|| DEFAULT_ACCENT.to_string());
    ThemeInfo { dark, accent }
}

#[tauri::command]
pub fn get_theme_info() -> ThemeInfo {
    get_system_theme()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn abgr_accent_becomes_css_hex() {
        assert_eq!(accent_from_abgr(0xFFD47800), "#0078D4");
        assert_eq!(accent_from_abgr(0xFF0000FF), "#FF0000");
        assert_eq!(accent_from_abgr(0xFF000000), "#000000");
    }

    #[test]
    fn system_theme_always_has_a_readable_accent() {
        let info = get_system_theme();
        assert!(info.accent.starts_with('#'));
        assert_eq!(info.accent.len(), 7);
    }
}
