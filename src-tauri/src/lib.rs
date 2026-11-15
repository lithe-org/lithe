pub mod appbar;
pub mod commands;
pub mod status;
pub mod window;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            commands::system::get_foreground_app,
            commands::system::get_system_status
        ])
        .setup(|app| {
            // 监听在独立线程跑消息循环，不阻塞状态栏启动。
            status::watcher::start(app.handle().clone());
            window::configure(app)?;
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
