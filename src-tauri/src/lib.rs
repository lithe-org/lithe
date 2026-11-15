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
            commands::system::get_foreground_app,
            commands::system::get_system_status
        ])
        .setup(|app| {
            // 监听在独立线程跑消息循环，不阻塞状态栏启动。
            status::watcher::start(app.handle().clone());
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
