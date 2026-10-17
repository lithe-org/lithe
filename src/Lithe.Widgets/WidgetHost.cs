using Lithe.Core.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace Lithe.Widgets;

/// <summary>
/// 桌面组件宿主：承载组件视图、负责网格定位、断点回调与编辑模式交互。
/// </summary>
public sealed class WidgetHost : UserControl
{
    /// <summary>
    /// 缩放应用的最小间隔（毫秒）。拖拽中连续 PointerMoved 会触发整条
    /// 布局链路，限频后每次拖动最多每秒十几次布局变更。
    /// </summary>
    private const long ResizeApplyIntervalMs = 60;

    private readonly ILayoutEngine _layout;
    private readonly IEditModeService? _editMode;
    private readonly WidgetSizeConstraint _constraint;
    private GridMetrics _metrics;
    private readonly FrameworkElement _content;
    private readonly Grid _handleHost;
    private readonly Border _resizeHandle;
    private WidgetPlacement _placement;
    private WidgetSizeClass _sizeClass;
    private double _originLeft;
    private double _originTop;

    // 编辑模式手势状态。处理器在构造时注册一次，用状态区分手势，
    // 避免每次按下都重复订阅 PointerMoved 导致事件风暴。
    private bool _isDragging;
    private bool _isResizing;
    private GridCell _gestureStartCell;
    private Point _gestureStartScreen;
    private double _dragOffsetX;
    private double _dragOffsetY;
    private long _lastResizeApplyTick;

    /// <summary>
    /// 创建组件宿主。
    /// </summary>
    /// <param name="view">组件视图。</param>
    /// <param name="definition">组件元数据。</param>
    /// <param name="placement">初始占位。</param>
    /// <param name="metrics">显示器网格指标。</param>
    /// <param name="layout">布局引擎。</param>
    /// <param name="editMode">编辑模式服务，可为空。</param>
    public WidgetHost(
        FrameworkElement view,
        IWidget definition,
        WidgetPlacement placement,
        GridMetrics metrics,
        ILayoutEngine layout,
        IEditModeService? editMode = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(layout);

        _content = view;
        _placement = placement;
        _metrics = metrics;
        _layout = layout;
        _editMode = editMode;
        _constraint = definition.SizeConstraint;

        Background = WidgetBackground.Create(WidgetBackgroundKind.Acrylic, Microsoft.UI.Colors.Black, 0.55);

        _resizeHandle = BuildResizeHandle();
        _handleHost = new Grid();
        _handleHost.Children.Add(_resizeHandle);
        _handleHost.IsHitTestVisible = false;

        var root = new Grid();
        root.Children.Add(_content);
        root.Children.Add(_handleHost);
        Content = root;

        ApplyPlacementGeometry();
        ApplyEditModeState();

        _content.SizeChanged += OnContentSizeChanged;
        if (editMode is not null)
        {
            editMode.EditModeChanged += OnEditModeChanged;
        }

        // 编辑模式：整块可拖拽移动，右下角把手缩放。非编辑模式不拦截，
        // 组件内部交互（打卡、添加待办）照常工作。
        PointerPressed += OnWidgetPointerPressed;
        PointerMoved += OnWidgetPointerMoved;
        PointerReleased += OnWidgetPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
    }

    /// <summary>
    /// 组件 Id。
    /// </summary>
    public string WidgetId => _placement.WidgetId;

    /// <summary>
    /// 当前占位。
    /// </summary>
    public WidgetPlacement Placement => _placement;

    /// <summary>
    /// 当前断点。
    /// </summary>
public WidgetSizeClass SizeClass => _sizeClass;

    /// <summary>
    /// 组件在工作区坐标系里的像素矩形，用于命中测试。
    /// </summary>
    public PixelRect PixelBounds => ToPixels(_placement.Cell);

    /// <summary>
    /// 设置承载窗口的原点。桌面层只覆盖组件包围盒，布局坐标需要减去这个原点。
    /// </summary>
    public void SetOrigin(double left, double top)
    {
        _originLeft = left;
        _originTop = top;
        ApplyPlacementGeometry();
    }

    /// <summary>
    /// 显示器网格变化时重新定位。
    /// </summary>
    public void UpdateMetrics(GridMetrics metrics, WidgetPlacement placement)
    {
        _metrics = metrics;
        _placement = placement;
        ApplyPlacementGeometry();
    }

    /// <summary>
    /// 设置背景类型。
    /// </summary>
    public void SetBackground(WidgetBackgroundKind kind, Color color, double opacity = 0.7)
    {
        Background = WidgetBackground.Create(kind, color, opacity);
    }

    private Grid BuildContentRoot()
    {
        var root = new Grid();
        root.Children.Add(_content);
        return root;
    }

    private Border BuildResizeHandle()
    {
        var handle = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(Colors.Black),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 6, 6),
            Visibility = Visibility.Collapsed,
        };

        handle.PointerPressed += OnResizeHandlePressed;
        handle.PointerMoved += OnResizeHandleMoved;
        handle.PointerReleased += OnResizeHandleReleased;
        handle.PointerCaptureLost += OnPointerCaptureLost;
        return handle;
    }

    private void ApplyPlacementGeometry()
    {
        var rect = ToPixels(_placement.Cell);

        Width = rect.Width;
        Height = rect.Height;

        // 拖拽中带上像素偏移，组件跟着光标走，松手才吸附回网格。
        Canvas.SetLeft(this, rect.Left - _originLeft + _dragOffsetX);
        Canvas.SetTop(this, rect.Top - _originTop + _dragOffsetY);
    }

    private Lithe.Core.Layout.PixelRect ToPixels(GridCell cell)
    {
        var left = _metrics.OriginLeft + (cell.Column * (_metrics.CellWidth + _metrics.Spacing));
        var top = _metrics.OriginTop + (cell.Row * (_metrics.CellHeight + _metrics.Spacing));
        var width = (cell.ColSpan * _metrics.CellWidth) + ((cell.ColSpan - 1) * _metrics.Spacing);
        var height = (cell.RowSpan * _metrics.CellHeight) + ((cell.RowSpan - 1) * _metrics.Spacing);
        return new Lithe.Core.Layout.PixelRect(left, top, width, height);
    }

    private void ApplyEditModeState()
    {
        var editing = _editMode?.IsEditing ?? false;

        // 编辑模式外，组件内部交互正常；编辑模式内整块变成可拖拽实体。
        _content.IsHitTestVisible = !editing;
        _handleHost.IsHitTestVisible = editing;
        _resizeHandle.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        Opacity = editing ? 0.75 : 1.0;
        BorderBrush = editing ? new SolidColorBrush(Colors.White with { A = 0xAA }) : null;
        BorderThickness = editing ? new Thickness(1) : new Thickness(0);
    }

    private void OnEditModeChanged(object? sender, EventArgs args) => ApplyEditModeState();

    private void OnContentSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var sizeClass = ResolveSizeClass(args.NewSize);

        if (_content is FrameworkElement { } view && view is IWidgetViewAware aware && sizeClass != _sizeClass)
        {
            aware.OnSizeClassChanged(sizeClass);
        }

        _sizeClass = sizeClass;
    }

    private static WidgetSizeClass ResolveSizeClass(Size size)
    {
        var area = size.Width * size.Height;

        if (size.Width < 220 || area < 24000)
        {
            return WidgetSizeClass.Compact;
        }

        return size.Width >= 420 && area >= 90000
            ? WidgetSizeClass.Expanded
            : WidgetSizeClass.Regular;
    }

    private void OnWidgetPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (_editMode?.IsEditing != true || _isDragging || _isResizing)
        {
            return;
        }

        // 编辑模式下组件内部不接收事件，整块就是一个可拖拽实体。
        args.Handled = true;
        CapturePointer(args.Pointer);

        _isDragging = true;
        _gestureStartCell = _placement.Cell;
        _gestureStartScreen = GetCursorScreenPoint();
        _dragOffsetX = 0;
        _dragOffsetY = 0;
    }

    private void OnWidgetPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!_isDragging)
        {
            return;
        }

        // 屏幕坐标做基准：窗口在编辑模式下铺满工作区不再移动，只有组件自身
        // 在动，局部坐标会随组件位置漂移，屏幕坐标才不会自激。
        var current = GetCursorScreenPoint();
        _dragOffsetX = current.X - _gestureStartScreen.X;
        _dragOffsetY = current.Y - _gestureStartScreen.Y;
        ApplyPlacementGeometry();
    }

    private void OnWidgetPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleasePointerCapture(args.Pointer);

        var metrics = _layout.Metrics;
        var stepX = metrics.CellWidth + metrics.Spacing;
        var stepY = metrics.CellHeight + metrics.Spacing;

        var column = _gestureStartCell.Column + (int)Math.Round(_dragOffsetX / stepX);
        var row = _gestureStartCell.Row + (int)Math.Round(_dragOffsetY / stepY);
        column = Math.Clamp(column, 0, Math.Max(0, metrics.Columns - _gestureStartCell.ColSpan));
        row = Math.Clamp(row, 0, Math.Max(0, metrics.Rows - _gestureStartCell.RowSpan));

        _dragOffsetX = 0;
        _dragOffsetY = 0;

        var target = _gestureStartCell with { Column = column, Row = row };
        if (target == _placement.Cell || !_layout.Place(_placement.WidgetId, target))
        {
            // 原地放下或目标被占，回弹到当前网格位置。
            ApplyPlacementGeometry();
        }
    }

    private void OnResizeHandlePressed(object sender, PointerRoutedEventArgs args)
    {
        if (_editMode?.IsEditing != true || _isDragging || _isResizing)
        {
            return;
        }

        args.Handled = true;
        var handle = (UIElement)sender;
        handle.CapturePointer(args.Pointer);

        _isResizing = true;
        _gestureStartCell = _placement.Cell;
        _gestureStartScreen = GetCursorScreenPoint();
        _lastResizeApplyTick = Environment.TickCount64;
    }

    private void OnResizeHandleMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!_isResizing)
        {
            return;
        }

        // 限频：PointerMoved 可以远快于鼠标回报率，每次都跑整条布局链路
        // 会把界面卡死，这里最多每 60ms 应用一次。
        var now = Environment.TickCount64;
        if (now - _lastResizeApplyTick < ResizeApplyIntervalMs)
        {
            return;
        }

        _lastResizeApplyTick = now;
        ApplyResizeFromCursor();
    }

    private void OnResizeHandleReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!_isResizing)
        {
            return;
        }

        _isResizing = false;
        ((UIElement)sender).ReleasePointerCapture(args.Pointer);

        // 节流跳过的中间跨度直接补上终态。
        ApplyResizeFromCursor();
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        // 捕获丢失（切窗口、Alt+Tab）时清掉手势状态，避免组件粘在光标上。
        if (_isDragging)
        {
            _isDragging = false;
            _dragOffsetX = 0;
            _dragOffsetY = 0;
            ApplyPlacementGeometry();
        }

        _isResizing = false;
    }

    /// <summary>
    /// 按光标相对按下点的位移计算目标跨度并应用。屏幕坐标基准，
    /// 组件自身缩放不会让坐标自激；放不下的跨度直接跳过。
    /// </summary>
    private void ApplyResizeFromCursor()
    {
        var current = GetCursorScreenPoint();
        var metrics = _layout.Metrics;
        var stepX = metrics.CellWidth + metrics.Spacing;
        var stepY = metrics.CellHeight + metrics.Spacing;

        var colSpan = _gestureStartCell.ColSpan + (int)Math.Round((current.X - _gestureStartScreen.X) / stepX);
        var rowSpan = _gestureStartCell.RowSpan + (int)Math.Round((current.Y - _gestureStartScreen.Y) / stepY);
        var (col, row) = _constraint.Clamp(colSpan, rowSpan);

        if (col == _placement.Cell.ColSpan && row == _placement.Cell.RowSpan)
        {
            return;
        }

        var target = _gestureStartCell with { ColSpan = col, RowSpan = row };
        if (!_layout.CanPlace(new WidgetPlacement(_placement.WidgetId, _gestureStartCell), target))
        {
            return;
        }

        _placement = _layout.Resize(_placement.WidgetId, col, row);
        ApplyPlacementGeometry();
    }

    private static Point GetCursorScreenPoint()
    {
        NativeMethods.GetCursorPos(out var point);
        return point;
    }

    /// <summary>
    /// 组件卸载：解除事件订阅，避免组件视图被回收后仍被回调。
    /// </summary>
public void Unload()
    {
        _content.SizeChanged -= OnContentSizeChanged;

        if (_editMode is not null)
        {
            _editMode.EditModeChanged -= OnEditModeChanged;
        }
    }

    /// <summary>
    /// 编辑模式下的键盘删除。
    /// </summary>
    public void HandleDeleteKey()
    {
        if (_editMode?.IsEditing == true)
        {
            _layout.Remove(_placement.WidgetId);
        }
    }

    /// <summary>
    /// 接收按键，Delete 键删除组件。
    /// </summary>
    protected override void OnKeyDown(KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Delete)
        {
            HandleDeleteKey();
            args.Handled = true;
            return;
        }

        base.OnKeyDown(args);
    }
}