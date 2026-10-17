using Lithe.Config;
using Lithe_App.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Lithe_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// 应用级配置服务。所有模块通过它读写配置，不要各自建实例。
    /// </summary>
    public static TomlConfigService Configuration { get; private set; } = null!;

    /// <summary>
    /// 桌面层窗口，承载桌面小组件。
    /// </summary>
    public static DesktopHost? DesktopLayer { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Configuration = Bootstrap.AppBootstrap.InitializeConfig();

        Diagnostics.StartupSelfCheck.Run();

        DesktopLayer = new DesktopHost();
        DesktopLayer.Show();

        // 验证桌面层挂载用的开关，正常运行不挂载。打包启动不继承环境变量，所以同时支持标记文件。
if (ShouldAttachWorkerW())
        {
            _ = DesktopLayer.AttachToDesktopLayerAsync();
        }

        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
    }

    private static bool ShouldAttachWorkerW() =>
        Environment.GetEnvironmentVariable("LITHE_ATTACH_WORKERW") == "1"
        || File.Exists(Path.Combine(ConfigPaths.RootDirectory, "attach-workerw.flag"));
}
