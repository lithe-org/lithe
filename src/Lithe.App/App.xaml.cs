using Lithe.Config;
using Lithe_App.Shell;
using Lithe.Data;
using Lithe.Layout;
using Lithe.Widgets;
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
    /// 数据源注册表。所有模块通过它取数据源，不要自行 new。
    /// </summary>
    public static DataSourceRegistry DataSources { get; private set; } = null!;

    /// <summary>
    /// 桌面组件布局引擎。
    /// </summary>
    public static LayoutEngine Layout { get; private set; } = null!;

    /// <summary>
    /// 桌面编辑模式。
    /// </summary>
    public static EditModeService EditMode { get; private set; } = null!;

    /// <summary>
    /// 组件注册表。
    /// </summary>
    public static WidgetRegistry Widgets { get; private set; } = null!;

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
        Diagnostics.ShellDiagnostics.Install();

        Configuration = Bootstrap.AppBootstrap.InitializeConfig();
        DataSources = Bootstrap.AppBootstrap.InitializeDataSources();

        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();

        (Layout, EditMode) = Bootstrap.AppBootstrap.InitializeLayout();
        Widgets = Bootstrap.AppBootstrap.InitializeWidgets();

        if (IsDesktopLayerEnabled())
        {
            DesktopLayer = new DesktopHost(
                Bootstrap.AppBootstrap.CreateWidgetContext(),
                Widgets,
                Layout,
                EditMode);
            DesktopLayer.Show();

            AddStarterWidgetsIfEmpty();

            // 桌面层挂载开关：默认用顶层窗口更稳，需要压到壁纸层时再打开。
            if (ShouldAttachDesktopLayer())
            {
                _ = DesktopLayer.AttachToDesktopLayerAsync();
            }
        }
        else
        {
            Lithe.Core.Diagnostics.AppLog.Info("桌面层已按配置禁用，只运行控制台");
        }

        // 自检里有等待，放后台跑，不拖慢启动。
        _ = Task.Run(Diagnostics.StartupSelfCheck.Run);
    }

    /// <summary>
    /// 桌面层可用配置项关闭，标记文件用于出问题时快速自救。
    /// </summary>
    private static bool IsDesktopLayerEnabled() =>
        Configuration.GetOrDefault("desktop_layer.enabled", true)
        && !File.Exists(Path.Combine(ConfigPaths.RootDirectory, "disable-desktop-layer.flag"));

    private static bool ShouldAttachDesktopLayer() =>
        Configuration.GetOrDefault("desktop_layer.attach_to_desktop_host", false)
        || File.Exists(Path.Combine(ConfigPaths.RootDirectory, "attach-desktop-layer.flag"));

    /// <summary>
    /// 首次启动桌面为空时放时钟与待办，方便验收；用户可以在桌面编辑模式里删掉。
    /// </summary>
    private void AddStarterWidgetsIfEmpty()
    {
        var desktop = DesktopLayer;
        if (desktop is null)
        {
            return;
        }

        if (Layout.GetAllPlacements().Count == 0)
        {
            desktop.AddWidget(Lithe.Widgets.Builtin.ClockWidget.WidgetId);
            desktop.AddWidget(Lithe.Widgets.Builtin.TodoWidget.WidgetId);
            return;
        }

        desktop.SyncWidgets();
    }
}
