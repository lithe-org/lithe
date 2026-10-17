using Lithe.Core.Layout;
using Lithe_App.Diagnostics;
using Lithe_App.Interop;
using Lithe.Widgets;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Lithe_App.Shell;

/// <summary>
/// 桌面层窗口：覆盖工作区的透明置顶窗口，承载桌面小组件。
/// </summary>
/// <remarks>
/// 窗口本身不抢焦点，不显示在任务栏；鼠标落在组件之外时整窗穿透，
/// 保证不影响桌面图标和其他程序的正常操作。
/// </remarks>
public sealed partial class DesktopHost
{
    private static DesktopHost? s_instance;

    private readonly Window _window;
    private readonly nint _hwnd;
    private readonly Canvas _widgetSurface;
    private readonly WidgetContext _widgetContext;
    private readonly WidgetRegistry _registry;
    private readonly ILayoutEngine _layout;
    private readonly IEditModeService _editMode;
    private readonly Dictionary<string, WidgetHost> _hosts = [];

    /// <summary>
    /// 创建桌面层窗口。
    /// </summary>
    public DesktopHost(WidgetContext widgetContext, WidgetRegistry registry, ILayoutEngine layout, IEditModeService editMode)
    {
        _widgetContext = widgetContext ?? throw new ArgumentNullException(nameof(widgetContext));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _editMode = editMode ?? throw new ArgumentNullException(nameof(editMode));

        // 不设置 SystemBackdrop：WinUI 3 在无背景时保留 DirectComposition 的透明区域，
        // 材质由各个卡片自己用 AcrylicBrush 等实现，这样桌面层本身不遮挡壁纸。
        _window = new Window
        {
            Title = "Lithe 桌面层",
        };

        _widgetSurface = new Canvas();
        var root = new Grid();
        root.Children.Add(_widgetSurface);
        _window.Content = root;

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

        ApplyOverlayPresenter();
        ApplyWindowStyles();
        _ = NativeMethods.SetWindowSubclass(_hwnd, SubclassCallback, uIdSubclass: 1, dwRefData: 0);

        _layout.LayoutChanged += OnLayoutChanged;
        _editMode.EditModeChanged += OnEditModeChanged;

        s_instance = this;
    }

    /// <summary>
    /// 当前桌面层窗口实例，未创建时为 <see langword="null"/>。
    /// </summary>
    public static DesktopHost? Instance => s_instance;

    /// <summary>
    /// 鼠标穿透模式。开启后整窗透明穿透，只有组件区域能接收鼠标。
    /// </summary>
    public bool ClickThrough { get; private set; } = true;

    /// <summary>
    /// 当前挂载状态描述。
    /// </summary>
    public string AttachState { get; private set; } = "顶层窗口";

    /// <summary>
    /// 显示桌面层窗口。
    /// </summary>
    public void Show()
    {
        _window.Activate();
        ApplyWorkAreaBounds();
        ShellDiagnostics.Write($"桌面层窗口 0x{_hwnd:X}，组件 {_hosts.Count} 个");
    }

    /// <summary>
    /// 把桌面层挂到桌面宿主窗口，使其位于桌面图标所在层。
    /// </summary>
    /// <remarks>
    /// 向 Explorer 发消息是同步的，会阻塞调用线程，所以查找放到后台线程，
    /// 窗口句柄的改动仍然回到 UI 线程执行。
    /// </remarks>
    public async Task<string> AttachToDesktopLayerAsync()
    {
        ShellDiagnostics.Write("开始查找桌面宿主窗口");

        var host = await Task.Run(DesktopLayerHost.FindDesktopHost);
        if (host == 0)
        {
            AttachState = "未找到桌面宿主窗口，保持顶层窗口";
            ShellDiagnostics.Write(AttachState);
            return AttachState;
        }

        AttachState = DesktopLayerHost.Attach(_hwnd, host)
            ? $"已挂载到桌面宿主 0x{host:X}"
            : $"挂载桌面宿主 0x{host:X} 失败，保持顶层窗口";

        ShellDiagnostics.Write(AttachState);
        return AttachState;
    }

    /// <summary>
    /// 从桌面宿主摘回顶层窗口。
    /// </summary>
    public string DetachFromDesktopLayer()
    {
        NativeMethods.SetParent(_hwnd, 0);

        var style = NativeMethods.GetStyle(_hwnd);
        NativeMethods.SetStyle(_hwnd, (style & ~NativeMethods.WS_CHILD) | NativeMethods.WS_POPUP);

        ApplyWorkAreaBounds();
        AttachState = "已摘回顶层窗口";
        return AttachState;
    }

    /// <summary>
    /// 切换鼠标穿透模式。
    /// </summary>
    public void ToggleClickThrough()
    {
        ClickThrough = !ClickThrough;
        ShellDiagnostics.Write($"鼠标穿透 {(ClickThrough ? "开启" : "关闭")}");
    }

    /// <summary>
    /// 按布局表同步桌面上的组件。
    /// </summary>
    public void SyncWidgets()
    {
        try
        {
            SyncWidgetsCore();
            ShellDiagnostics.Write($"桌面组件已同步：{(string.Join(", ", _hosts.Keys))}");
        }
        catch (Exception ex)
        {
            // 单个组件加载失败不能让整个桌面层挂掉。
            ShellDiagnostics.Write($"桌面组件同步失败：{ex}");
        }
    }

    private void SyncWidgetsCore()
    {
        var placements = _layout.GetAllPlacements();

        // 移除已经不存在的组件。
        foreach (var id in _hosts.Keys.ToList())
        {
            if (placements.All(p => !string.Equals(p.WidgetId, id, StringComparison.Ordinal)))
            {
                _hosts[id].Unload();
                _widgetSurface.Children.Remove(_hosts[id]);
                _hosts.Remove(id);
            }
        }

        foreach (var placement in placements)
        {
            if (_hosts.TryGetValue(placement.WidgetId, out var host))
            {
                host.UpdateMetrics(_layout.Metrics, placement);
                continue;
            }

            if (!_registry.TryGet(placement.WidgetId, out var factory))
            {
                ShellDiagnostics.Write($"布局中的组件 {placement.WidgetId} 没有对应实现，已跳过");
                continue;
            }

            var view = factory.CreateView(WidgetViewMode.Desktop, _widgetContext);
            var created = new WidgetHost(view, factory.Definition, placement, _layout.Metrics, _layout, _editMode);
            _widgetSurface.Children.Add(created);
            _hosts[placement.WidgetId] = created;
            ApplyAppearanceCore(placement.WidgetId);
        }
    }

    /// <summary>
    /// 添加组件，自动寻找第一个能放下的位置。
    /// </summary>
    /// <param name="widgetId">组件 Id。</param>
    /// <param name="editMode">是否要求组件不超过默认尺寸。默认只追加、不改动已有占位。</param>
    public bool AddWidget(string widgetId)
    {
        if (!_registry.TryGet(widgetId, out var factory))
        {
            ShellDiagnostics.Write($"未知组件 {widgetId}");
            return false;
        }

        var constraint = factory.Definition.SizeConstraint;
        var cell = FindFreeCell(constraint);
        if (cell is null)
        {
            ShellDiagnostics.Write($"桌面没有空位放置 {widgetId}");
            return false;
        }

        if (!_layout.Place(widgetId, cell.Value))
        {
            return false;
        }

        _layout.RegisterWidget(widgetId, constraint);

        // Place 会触发 LayoutChanged，组件随之同步，这里不用重复同步。
        return true;
    }

    /// <summary>
    /// 删除组件。
    /// </summary>
    public bool RemoveWidget(string widgetId) => _layout.Remove(widgetId);

    /// <summary>
    /// 应用组件外观配置。
    /// </summary>
    /// <param name="widgetId">组件 Id。</param>
    /// <param name="appearance">外观设置，传 <see langword="null"/> 表示重新读取配置。</param>
    public bool ApplyAppearance(string widgetId, WidgetAppearance? appearance = null)
    {
        if (!_hosts.TryGetValue(widgetId, out var host))
        {
            return false;
        }

        var config = appearance ?? WidgetAppearanceService.Load(_widgetContext.Config, widgetId);
        host.SetBackground(config.Background, config.ResolveColor(), Math.Clamp(config.Opacity, 0, 1));

        ShellDiagnostics.Write($"组件 {widgetId} 外观：{config.Background} {config.ColorHex} {config.Opacity:P0}");
        return true;
    }

    /// <summary>
    /// 当前状态描述。
    /// </summary>
    public string DescribeState() =>
        $"桌面层 0x{_hwnd:X}，组件 {_hosts.Count} 个，网格 {_layout.Metrics.Columns}x{_layout.Metrics.Rows}，穿透 {(ClickThrough ? "开" : "关")}，{AttachState}";

    private void ApplyAppearanceCore(string widgetId) => ApplyAppearance(widgetId);

    private void ApplyOverlayPresenter()
    {
        var appWindow = _window.AppWindow;
        appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }

    /// <summary>
    /// 铺满主显示器工作区。必须在窗口显示之后调用，否则尺寸设置会被忽略。
    /// </summary>
    private void ApplyWorkAreaBounds()
    {
        var appWindow = _window.AppWindow;
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        appWindow.MoveAndResize(new RectInt32(workArea.X, workArea.Y, workArea.Width, workArea.Height));
    }

    private void ApplyWindowStyles()
    {
        var exStyle = NativeMethods.GetExStyle(_hwnd);
        exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetExStyle(_hwnd, exStyle);
    }

    private void OnLayoutChanged(object? sender, EventArgs args) => SyncWidgets();

    private void OnEditModeChanged(object? sender, EventArgs args)
    {
        // 编辑模式下允许组件接收鼠标（拖拽与缩放）。
        ShellDiagnostics.Write(_editMode.IsEditing ? "桌面层进入编辑模式" : "桌面层退出编辑模式");
    }

    private GridCell? FindFreeCell(WidgetSizeConstraint constraint)
    {
        var metrics = _layout.Metrics;
        var colSpan = Math.Clamp(constraint.DefaultColSpan, 1, metrics.Columns);
        var rowSpan = Math.Clamp(constraint.DefaultRowSpan, 1, metrics.Rows);

        for (var row = 0; row <= metrics.Rows - rowSpan; row++)
        {
            for (var column = 0; column <= metrics.Columns - colSpan; column++)
            {
                var cell = new GridCell(column, row, colSpan, rowSpan);
                if (_layout.CanPlace(new WidgetPlacement(string.Empty, cell), cell))
                {
                    return cell;
                }
            }
        }

        return null;
    }

    private static nint SubclassCallback(nint hWnd, uint msg, nint wParam, nint lParam, nint uIdSubclass, nint dwRefData)
    {
        if (msg == NativeMethods.WM_NCHITTEST && s_instance is { ClickThrough: true } host)
        {
            return host.IsOutsideInteractiveRegion(hWnd, lParam)
                ? NativeMethods.HTTRANSPARENT
                : NativeMethods.HTCLIENT;
        }

        return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private bool IsOutsideInteractiveRegion(nint hWnd, nint lParam)
    {
        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return true;
        }

        var point = cursor;
        if (!NativeMethods.ScreenToClient(hWnd, ref point))
        {
            return true;
        }

        if (_editMode.IsEditing)
        {
            return false;
        }

        // 编辑模式外只允许组件区域接收鼠标，其余部分整窗穿透。
        foreach (var host in _hosts.Values)
        {
            var rect = host.Placement.Cell;
            var metrics = _layout.Metrics;
            var left = metrics.OriginLeft + (rect.Column * (metrics.CellWidth + metrics.Spacing));
            var top = metrics.OriginTop + (rect.Row * (metrics.CellHeight + metrics.Spacing));
            var width = (rect.ColSpan * metrics.CellWidth) + ((rect.ColSpan - 1) * metrics.Spacing);
            var height = (rect.RowSpan * metrics.CellHeight) + ((rect.RowSpan - 1) * metrics.Spacing);

            if (point.X >= left && point.X <= left + width && point.Y >= top && point.Y <= top + height)
            {
                return false;
            }
        }

        return true;
    }
}