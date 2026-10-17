using Lithe.Core.Layout;
using Lithe_App.Diagnostics;
using Lithe_App.Interop;
using Lithe.Widgets;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

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
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;

    /// <summary>
    /// 编辑模式下的遮罩颜色，用来提示当前处于编辑状态。
    /// </summary>
    private static readonly Color s_editMaskColor = Color.FromArgb(0x66, 0, 0, 0);

    private static DesktopHost? s_instance;

    /// <summary>
    /// 子类化委托必须被强引用持有，否则被 GC 后命中测试会静默失效。
    /// </summary>
    private static readonly NativeMethods.SubclassProc s_subclassCallback = SubclassCallback;

    private readonly Window _window;
    private readonly nint _hwnd;
    private readonly Canvas _widgetSurface;
    private readonly WidgetContext _widgetContext;
    private readonly WidgetRegistry _registry;
    private readonly ILayoutEngine _layout;
    private readonly IEditModeService _editMode;
    private readonly Dictionary<string, WidgetHost> _hosts = [];
    private bool _hitTestReady;
    private bool _hasRendered;
    private PixelRect _windowArea;

    /// <summary>
    /// 创建桌面层窗口。
    /// </summary>
    public DesktopHost(WidgetContext widgetContext, WidgetRegistry registry, ILayoutEngine layout, IEditModeService editMode)
    {
        _widgetContext = widgetContext ?? throw new ArgumentNullException(nameof(widgetContext));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _editMode = editMode ?? throw new ArgumentNullException(nameof(editMode));

        // 不设置 SystemBackdrop：SDK 只有 Mica/DesktopAcrylic，没有透明 backdrop，
        // 窗口默认背景是不透明的，未覆盖区域会露底，所以根背景用与组件一致的
        // 亚克力兜底，编辑模式再盖半透明遮罩。
        _window = new Window
        {
            Title = "Lithe 桌面层",
        };

        _widgetSurface = new Canvas();
        var root = new Grid();
        root.Background = CreateWindowBackground();
        root.Children.Add(_widgetSurface);
        root.Loaded += OnRootLoaded;
        _window.Content = root;

        // 编辑模式下点空白处退出，避免误入编辑模式后整屏都点不到别的窗口。
        root.PointerPressed += OnRootPointerPressed;

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

        ApplyOverlayPresenter();
        ApplyWindowStyles();
        _hitTestReady = NativeMethods.SetWindowSubclass(_hwnd, s_subclassCallback, uIdSubclass: 1, dwRefData: 0);

        if (_hitTestReady)
        {
            ShellDiagnostics.Write("桌面层命中测试已启用：组件区域可交互，其余穿透");
        }
        else
        {
            // 命中测试不可用时退化为整窗穿透，至少不挡住用户操作。
            NativeMethods.SetExStyle(_hwnd, NativeMethods.GetExStyle(_hwnd) | NativeMethods.WS_EX_TRANSPARENT);
            ShellDiagnostics.Write("桌面层命中测试安装失败，已降级为整窗鼠标穿透");
        }

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
        UpdateWindowBounds();
        ShellDiagnostics.Write($"桌面层窗口 0x{_hwnd:X}，组件 {_hosts.Count} 个，覆盖区域 {_windowArea}");
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

        // 摘回顶层后恢复组件包围盒：铺满工作区会留下全屏置顶窗口挡住桌面。
        UpdateWindowBounds();
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
            UpdateWindowBounds();
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
    /// 只让窗口覆盖组件所在的矩形，避免空白全屏挡住其他程序。
    /// </summary>
    /// <remarks>
    /// 编辑模式为了方便拖拽会临时铺满工作区；桌面一个组件都没有时直接隐藏窗口。
    /// </remarks>
    private void UpdateWindowBounds()
    {
        var workArea = GetWorkArea();

        var target = _editMode.IsEditing
            ? new PixelRect(workArea.X, workArea.Y, workArea.Width, workArea.Height)
            : GetComponentBounds(workArea);

        if (target.Width <= 0 || target.Height <= 0)
        {
            // 没有组件可显示。首帧渲染前不能隐藏：提前 Hide 会让 XAML 内容
            // 永不呈现，窗口只剩不透明默认背景。渲染完成且非编辑模式才隐藏。
            if (_hasRendered && _hosts.Count == 0 && !_editMode.IsEditing)
            {
                NativeMethods.ShowWindow(_hwnd, SwHide);
                _windowArea = new PixelRect(workArea.X, workArea.Y, 0, 0);
            }

            return;
        }

        _windowArea = target;

        var appWindow = _window.AppWindow;
        appWindow.MoveAndResize(new RectInt32((int)target.Left, (int)target.Top, (int)target.Width, (int)target.Height));

        // 组件坐标是工作区坐标，窗口移动后要减去窗口原点。
        foreach (var host in _hosts.Values)
        {
            host.SetOrigin(target.Left, target.Top);
        }

        NativeMethods.ShowWindow(_hwnd, SwShowNoActivate);
    }

    /// <summary>
    /// 取所有组件的并集矩形。
    /// </summary>
    private PixelRect GetComponentBounds(RectInt32 workArea)
    {
        var metrics = _layout.Metrics;

        var left = double.MaxValue;
        var top = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MinValue;

        foreach (var placement in _layout.GetAllPlacements())
        {
            var rect = ToPixels(metrics, placement.Cell);
            left = Math.Min(left, rect.Left);
            top = Math.Min(top, rect.Top);
            right = Math.Max(right, rect.Right);
            bottom = Math.Max(bottom, rect.Bottom);
        }

        if (right <= left || bottom <= top)
        {
            return new PixelRect(workArea.X, workArea.Y, 0, 0);
        }

        // 不留边距：窗口紧贴组件包围盒，窗口默认背景就不会露出来。
        var x = (int)Math.Max(workArea.X, left);
        var y = (int)Math.Max(workArea.Y, top);
        var rightEdge = (int)Math.Min(workArea.X + workArea.Width, right);
        var bottomEdge = (int)Math.Min(workArea.Y + workArea.Height, bottom);

        return new PixelRect(x, y, Math.Max(0, rightEdge - x), Math.Max(0, bottomEdge - y));
    }

    private RectInt32? _lastWorkArea;

    private RectInt32 GetWorkArea()
    {
        // 挂到 Progman 后 DisplayArea.GetFromWindowId 可能返回 null 或抛异常，
        // 用最后一次有效值兜底，再不行退回主显示器工作区。
        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_window.AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea is not null)
            {
                _lastWorkArea = displayArea.WorkArea;
                return _lastWorkArea.Value;
            }
        }
        catch (Exception)
        {
        }

        return _lastWorkArea ?? DisplayArea.Primary.WorkArea;
    }

    /// <summary>
    /// 网格单元换算成工作区像素矩形。
    /// </summary>
    internal static PixelRect ToPixels(GridMetrics metrics, GridCell cell)
    {
        var left = metrics.OriginLeft + (cell.Column * (metrics.CellWidth + metrics.Spacing));
        var top = metrics.OriginTop + (cell.Row * (metrics.CellHeight + metrics.Spacing));
        var width = (cell.ColSpan * metrics.CellWidth) + ((cell.ColSpan - 1) * metrics.Spacing);
        var height = (cell.RowSpan * metrics.CellHeight) + ((cell.RowSpan - 1) * metrics.Spacing);
        return new PixelRect(left, top, width, height);
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
        // 编辑模式下允许组件接收鼠标（拖拽与缩放），并临时铺满工作区方便操作。
        ShellDiagnostics.Write(_editMode.IsEditing ? "桌面层进入编辑模式" : "桌面层退出编辑模式");
        ApplyEditModeMask();
        UpdateWindowBounds();
    }

    /// <summary>
    /// 编辑模式铺满工作区时给一层半透明遮罩，平时保持与组件一致的亚克力根背景。
    /// </summary>
    private void ApplyEditModeMask()
    {
        if (_window.Content is Grid root)
        {
            root.Background = _editMode.IsEditing
                ? new SolidColorBrush(s_editMaskColor)
                : CreateWindowBackground();
        }
    }

    /// <summary>
    /// 窗口内容根背景。SDK 没有透明 backdrop，窗口默认背景不透明，
    /// 用与组件一致的亚克力兜底，未覆盖区域也不会露出白底。
    /// </summary>
    private static Brush CreateWindowBackground() =>
        WidgetBackground.Create(WidgetBackgroundKind.Acrylic, Color.FromArgb(0xFF, 0x20, 0x20, 0x20), 0.7);

    /// <summary>
    /// 首帧渲染完成。WinUI 窗口在渲染前设置的边界可能被丢弃，这里补一次；
    /// 渲染完成前也不能隐藏窗口，否则 XAML 内容可能永不呈现。
    /// </summary>
    private void OnRootLoaded(object sender, RoutedEventArgs args)
    {
        if (_hasRendered)
        {
            return;
        }

        _hasRendered = true;
        UpdateWindowBounds();
        ShellDiagnostics.Write($"桌面层首帧已渲染：窗口 {_windowArea}，组件 {_hosts.Count} 个");
    }

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (!_editMode.IsEditing || _window.Content is not Grid root)
        {
            return;
        }

        var point = args.GetCurrentPoint(root).Position;
        foreach (var host in _hosts.Values)
        {
            var rect = host.PixelBounds;
            if (point.X >= rect.Left && point.X <= rect.Right && point.Y >= rect.Top && point.Y <= rect.Bottom)
            {
                return;
            }
        }

        args.Handled = true;
        _editMode.Exit();
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
            return host.IsOutsideInteractiveRegion(hWnd)
                ? NativeMethods.HTTRANSPARENT
                : NativeMethods.HTCLIENT;
        }

        return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// 判断鼠标是否落在所有可交互区域之外。
    /// </summary>
    private bool IsOutsideInteractiveRegion(nint hWnd)
    {
        if (_hosts.Count == 0)
        {
            return true;
        }

        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return true;
        }

        var point = cursor;
        if (!NativeMethods.ScreenToClient(hWnd, ref point))
        {
            return true;
        }

        // 编辑模式铺满工作区，整窗都用于拖拽。
        if (_editMode.IsEditing)
        {
            return false;
        }

        // 只允许组件矩形接收鼠标，其余区域整窗穿透。
        foreach (var host in _hosts.Values)
        {
            var rect = host.PixelBounds;
            if (point.X >= rect.Left && point.X <= rect.Right && point.Y >= rect.Top && point.Y <= rect.Bottom)
            {
                return false;
            }
        }

        return true;
    }
}