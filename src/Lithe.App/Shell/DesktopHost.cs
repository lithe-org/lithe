using System.Diagnostics;
using Lithe_App.Interop;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace Lithe_App.Shell;

/// <summary>
/// 桌面层窗口：覆盖整屏的透明置顶窗口，用来承载桌面小组件与后续的编辑模式交互。
/// </summary>
/// <remarks>
/// 窗口本身不抢焦点，不显示在任务栏，鼠标落在测试块之外时整窗穿透，
/// 保证不影响桌面图标和其他程序的正常操作。
/// </remarks>
public sealed partial class DesktopHost
{
    private const double BlockWidth = 360;
    private const double BlockHeight = 200;
    private const double BlockMargin = 24;

    private static DesktopHost? s_instance;

    private readonly Window _window;
    private readonly nint _hwnd;
    private readonly Border _testBlock;
    private readonly TextBlock _hintText;
    private Rect _interactiveRegion;

    /// <summary>
    /// 当前桌面层窗口实例，未创建时为 <see langword="null"/>。
    /// </summary>
    public static DesktopHost? Instance => s_instance;

    /// <summary>
    /// 创建桌面层窗口。
    /// </summary>
    public DesktopHost()
    {
        // 不设置 SystemBackdrop：WinUI 3 在无背景时保留 DirectComposition 的透明区域，
        // 材质由各个卡片自己用 AcrylicBrush 等实现，这样桌面层本身不遮挡壁纸。
        _window = new Window();

        var root = new Grid();
        var canvas = new Canvas();
        canvas.Children.Add(BuildTestBlock(out _testBlock, out _hintText));
        root.Children.Add(canvas);
        _window.Content = root;

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

        ApplyOverlayPresenter();
        ApplyWindowStyles();
        _ = NativeMethods.SetWindowSubclass(_hwnd, SubclassCallback, uIdSubclass: 1, dwRefData: 0);

        root.Loaded += OnRootLoaded;
        root.SizeChanged += OnRootSizeChanged;

        s_instance = this;
    }

    /// <summary>
    /// 显示桌面层窗口。
    /// </summary>
    public void Show()
    {
        _window.Activate();
        Debug.WriteLine($"[桌面层] 窗口句柄 0x{_hwnd:X}，命中区域 {_interactiveRegion}");
    }

    /// <summary>
    /// 鼠标穿透模式。开启后整窗透明穿透，只有测试块区域能接收鼠标。
    /// </summary>
    public bool ClickThrough { get; private set; } = true;

    /// <summary>
    /// 切换鼠标穿透模式。
    /// </summary>
    public void ToggleClickThrough()
    {
        ClickThrough = !ClickThrough;
        UpdateBlockHint();
    }

    /// <summary>
    /// 当前状态描述，供设置界面与日志使用。
    /// </summary>
    public string DescribeState() =>
        $"桌面层 0x{_hwnd:X}，命中区域 {DescribeRect(_interactiveRegion)}，穿透 {(ClickThrough ? "开" : "关")}";

    private Border BuildTestBlock(out Border block, out TextBlock hintText)
    {
        hintText = new TextBlock
        {
            Text = "GPU 合成正常，块外区域鼠标穿透。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        };

        block = new Border
        {
            Width = BlockWidth,
            Height = BlockHeight,
            CornerRadius = new CornerRadius(12),
            Background = new AcrylicBrush
            {
                TintColor = Color.FromArgb(0xCC, 0x20, 0x20, 0x20),
                TintOpacity = 0.6,
                FallbackColor = Color.FromArgb(0xCC, 0x20, 0x20, 0x20),
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(16),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Lithe 桌面层测试块",
                        Foreground = new SolidColorBrush(Colors.White),
                        Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
                    },
                    hintText,
                    new Button
                    {
                        Content = "命中测试",
                        HorizontalAlignment = HorizontalAlignment.Left,
                    },
                },
            },
        };

        return block;
    }

    private void ApplyOverlayPresenter()
    {
        var appWindow = _window.AppWindow;
        appWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);

        // 桌面层常驻在桌面之上，但不参与任务栏与 Alt-Tab。
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }
    }

    private void ApplyWindowStyles()
    {
        var exStyle = NativeMethods.GetExStyle(_hwnd);
        exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetExStyle(_hwnd, exStyle);
    }

    private void OnRootLoaded(object sender, RoutedEventArgs args)
    {
        LayoutTestBlock();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs args)
    {
        LayoutTestBlock();
    }

    private void LayoutTestBlock()
    {
        if (_window.Content is not Grid root || root.ActualWidth <= 0 || root.ActualHeight <= 0)
        {
            return;
        }

        var left = root.ActualWidth - BlockWidth - BlockMargin;
        var top = root.ActualHeight - BlockHeight - BlockMargin;

        Canvas.SetLeft(_testBlock, left);
        Canvas.SetTop(_testBlock, top);

        _interactiveRegion = new Rect(
            (int)left,
            (int)top,
            (int)(left + BlockWidth),
            (int)(top + BlockHeight));
    }

    private void UpdateBlockHint()
    {
        _hintText.Text = ClickThrough
            ? "GPU 合成正常，块外区域鼠标穿透。"
            : "穿透已关闭，整个桌面层都会接收鼠标。";
    }

    private static string DescribeRect(Rect rect) => $"({rect.Left}, {rect.Top})-({rect.Right}, {rect.Bottom})";

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

        return point.X < _interactiveRegion.Left
            || point.X > _interactiveRegion.Right
            || point.Y < _interactiveRegion.Top
            || point.Y > _interactiveRegion.Bottom;
    }
}