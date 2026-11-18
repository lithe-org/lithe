pub mod appbar;
pub mod commands;
pub mod status;
pub mod window;

use tauri::Manager;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let app = tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            commands::popup::hide_popup,
            commands::popup::resize_popup,
            commands::popup::show_popup,
            commands::power::lock_workstation,
            commands::power::open_system_info,
            commands::power::open_task_manager,
            commands::power::open_windows_settings,
            commands::power::restart,
            commands::power::shutdown,
            commands::power::sleep,
            commands::system::get_foreground_app,
            commands::system::get_system_status,
            status::resource::get_resource_usage,
            status::wifi::get_wifi_status,
            status::wifi::list_wifi_networks,
            commands::window_control::apply_split,
            commands::window_control::capture_control_target,
            commands::window_control::is_controllable_foreground,
            commands::window_control::minimize_foreground,
            commands::window_control::toggle_maximize,
            commands::window_control::toggle_topmost,
            status::audio::get_volume,
            status::audio::set_volume,
            status::audio::toggle_mute,
            status::battery::get_battery_status
        ])
        .setup(|app| {
            // 监听在独立线程跑消息循环，不阻塞状态栏启动。
            status::watcher::start(app.handle().clone());
            status::resource::start(app.handle().clone());
            status::wifi::start(app.handle().clone());
            status::audio::start(app.handle().clone());
            status::battery::start(app.handle().clone());
            window::configure(app)?;
            // 预创建下拉窗口，避免首次点击时现建 WebView 的可见延迟。
            app.manage(window::popup_pool::PopupPool::new(app.handle())?);
            Ok(())
        })
        .build(tauri::generate_context!())
        .expect("error while building tauri application");

    app.run(|_app_handle, event| {
        // 正常退出必须注销 AppBar，否则工作区顶部会永久留白；
        // 进程被强杀时无法清理，列为已知限制（PRD 13 章记录）。
        if matches!(
            event,
            tauri::RunEvent::ExitRequested { .. } | tauri::RunEvent::Exit
        ) {
            appbar::unregister_registered();
        }
    });
}
