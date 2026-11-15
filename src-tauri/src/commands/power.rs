use std::process::Command;
use windows_sys::Win32::System::Power::SetSuspendState;
use windows_sys::Win32::System::Shutdown::LockWorkStation;

/// PRD 5.2.1 的七个系统动作。把「怎么执行」抽成纯数据，才能逐项断言行为。
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PowerAction {
    SystemInfo,
    WindowsSettings,
    Lock,
    Sleep,
    Restart,
    Shutdown,
    TaskManager,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum NativeAction {
    Lock,
    Sleep,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ActionSpec {
    Uri(&'static str),
    Command(&'static str, &'static [&'static str]),
    Native(NativeAction),
}

impl PowerAction {
    pub fn spec(self) -> ActionSpec {
        match self {
            Self::SystemInfo => ActionSpec::Uri("ms-settings:about"),
            Self::WindowsSettings => ActionSpec::Uri("ms-settings:"),
            Self::Lock => ActionSpec::Native(NativeAction::Lock),
            Self::Sleep => ActionSpec::Native(NativeAction::Sleep),
            Self::Restart => ActionSpec::Command("shutdown", &["/r", "/t", "0"]),
            Self::Shutdown => ActionSpec::Command("shutdown", &["/s", "/t", "0"]),
            Self::TaskManager => ActionSpec::Command("taskmgr", &[]),
        }
    }
}

pub fn perform(action: PowerAction) -> Result<(), String> {
    match action.spec() {
        ActionSpec::Uri(uri) => open_uri(uri),
        ActionSpec::Command(program, args) => spawn(program, args),
        ActionSpec::Native(NativeAction::Lock) => lock(),
        ActionSpec::Native(NativeAction::Sleep) => suspend(),
    }
}

/// `start` 的第一个参数是窗口标题，必须给空串，否则 URI 会被当成标题吞掉。
fn open_uri(uri: &str) -> Result<(), String> {
    spawn("cmd", &["/C", "start", "", uri])
}

fn spawn(program: &str, args: &[&str]) -> Result<(), String> {
    Command::new(program)
        .args(args)
        .spawn()
        .map(|_| ())
        .map_err(|error| error.to_string())
}

fn lock() -> Result<(), String> {
    let locked = unsafe { LockWorkStation() };
    if locked == 0 {
        return Err("锁定工作站失败".to_string());
    }
    Ok(())
}

fn suspend() -> Result<(), String> {
    // 不强制挂起、不忽略唤醒事件，保持与系统默认行为一致。
    let suspended = unsafe { SetSuspendState(false, false, false) };
    if !suspended {
        return Err("进入睡眠失败".to_string());
    }
    Ok(())
}

#[tauri::command]
pub fn open_system_info() -> Result<(), String> {
    perform(PowerAction::SystemInfo)
}

#[tauri::command]
pub fn open_windows_settings() -> Result<(), String> {
    perform(PowerAction::WindowsSettings)
}

#[tauri::command]
pub fn lock_workstation() -> Result<(), String> {
    perform(PowerAction::Lock)
}

#[tauri::command]
pub fn sleep() -> Result<(), String> {
    perform(PowerAction::Sleep)
}

#[tauri::command]
pub fn restart() -> Result<(), String> {
    perform(PowerAction::Restart)
}

#[tauri::command]
pub fn shutdown() -> Result<(), String> {
    perform(PowerAction::Shutdown)
}

#[tauri::command]
pub fn open_task_manager() -> Result<(), String> {
    perform(PowerAction::TaskManager)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn system_info_opens_the_about_page_not_the_settings_home() {
        // 「系统信息」必须落到 about 页，落到设置首页就和下一项重复了。
        assert_eq!(
            PowerAction::SystemInfo.spec(),
            ActionSpec::Uri("ms-settings:about")
        );
    }

    #[test]
    fn windows_settings_opens_the_settings_home() {
        assert_eq!(
            PowerAction::WindowsSettings.spec(),
            ActionSpec::Uri("ms-settings:")
        );
    }

    #[test]
    fn restart_and_shutdown_use_zero_delay_and_the_right_switch() {
        assert_eq!(
            PowerAction::Restart.spec(),
            ActionSpec::Command("shutdown", &["/r", "/t", "0"])
        );
        assert_eq!(
            PowerAction::Shutdown.spec(),
            ActionSpec::Command("shutdown", &["/s", "/t", "0"])
        );
    }

    #[test]
    fn task_manager_spawns_taskmgr_without_arguments() {
        assert_eq!(
            PowerAction::TaskManager.spec(),
            ActionSpec::Command("taskmgr", &[])
        );
    }

    #[test]
    fn lock_and_sleep_stay_on_native_calls() {
        // 这两个动作没有可靠的命令行等价物，必须走 Win32。
        assert_eq!(
            PowerAction::Lock.spec(),
            ActionSpec::Native(NativeAction::Lock)
        );
        assert_eq!(
            PowerAction::Sleep.spec(),
            ActionSpec::Native(NativeAction::Sleep)
        );
    }
}
