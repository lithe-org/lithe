use windows_sys::Win32::UI::Input::KeyboardAndMouse::{
    SendInput, INPUT, INPUT_KEYBOARD, KEYBDINPUT, KEYEVENTF_KEYUP, VK_LWIN, VK_N,
};

/// Win+N 的两个键码；单独抽出来便于断言，防止以后误改成别的 Flyout 热键。
pub fn hotkey_sequence() -> [u16; 2] {
    [VK_LWIN, VK_N]
}

fn key_input(vk: u16, keyup: bool) -> INPUT {
    let mut input: INPUT = unsafe { std::mem::zeroed() };
    input.r#type = INPUT_KEYBOARD;
    input.Anonymous.ki = KEYBDINPUT {
        wVk: vk,
        wScan: 0,
        dwFlags: if keyup { KEYEVENTF_KEYUP } else { 0 },
        time: 0,
        dwExtraInfo: 0,
    };
    input
}

#[tauri::command]
pub fn open_notification_center() -> Result<(), String> {
    let [win, letter] = hotkey_sequence();
    let inputs = [
        key_input(win, false),
        key_input(letter, false),
        key_input(letter, true),
        key_input(win, true),
    ];
    let inserted = unsafe {
        SendInput(
            inputs.len() as u32,
            inputs.as_ptr(),
            std::mem::size_of::<INPUT>() as i32,
        )
    };
    if inserted != inputs.len() as u32 {
        return Err("发送 Win+N 失败".to_string());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn notification_hotkey_stays_win_plus_n() {
        // 0x5B = VK_LWIN，0x4E = VK_N；换成别的字母会打开不同的系统面板。
        assert_eq!(hotkey_sequence(), [0x5B, 0x4E]);
    }

    #[test]
    fn keyup_variant_sets_the_release_flag() {
        unsafe {
            assert_ne!(
                key_input(VK_N, true).Anonymous.ki.dwFlags & KEYEVENTF_KEYUP,
                0
            );
            assert_eq!(
                key_input(VK_N, false).Anonymous.ki.dwFlags & KEYEVENTF_KEYUP,
                0
            );
        }
    }
}
