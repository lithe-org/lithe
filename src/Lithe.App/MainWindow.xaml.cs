using Lithe_App.Shell;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Lithe_App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));

        // 关闭按钮只隐藏窗口，进程保活（桌面组件继续显示）；
        // 托盘菜单点"退出"时 IsExiting 为真，这里放行。
        AppWindow.Closing += OnClosing;
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (TrayIcon.IsExiting)
        {
            return;
        }

        args.Cancel = true;
        TrayIcon.HideWindow(this);
    }
}
