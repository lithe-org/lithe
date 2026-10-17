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
    private readonly ILayoutEngine _layout;
    private readonly IEditModeService? _editMode;
    private readonly WidgetSizeConstraint _constraint;
    private GridMetrics _metrics;
    private readonly FrameworkElement _content;
    private readonly Grid _handleHost;
    private readonly Border _resizeHandle;
    private WidgetPlacement _placement;
    private WidgetSizeClass _sizeClass;

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
        return handle;
    }

    private void ApplyPlacementGeometry()
    {
        var rect = ToPixels(_placement.Cell);

        Width = rect.Width;
        Height = rect.Height;
        Canvas.SetLeft(this, rect.Left);
        Canvas.SetTop(this, rect.Top);
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

    private void OnResizeHandlePressed(object sender, PointerRoutedEventArgs args)
    {
        if (_editMode?.IsEditing != true)
        {
            return;
        }

        args.Handled = true;
        var point = args.GetCurrentPoint(this).Position;
        var handle = (UIElement)sender;
        handle.CapturePointer(args.Pointer);

        var startCell = _placement.Cell;
        var startPoint = point;

        var move = new PointerEventHandler((s, e) =>
        {
            var current = e.GetCurrentPoint(this).Position;
            var deltaX = current.X - startPoint.X;
            var deltaY = current.Y - startPoint.Y;

            var colSpan = startCell.ColSpan + (int)Math.Round(deltaX / (_metrics.CellWidth + _metrics.Spacing));
            var rowSpan = startCell.RowSpan + (int)Math.Round(deltaY / (_metrics.CellHeight + _metrics.Spacing));

            var (col, row) = _constraint.Clamp(colSpan, rowSpan);
            var target = startCell with { ColSpan = col, RowSpan = row };

            if (_layout.CanPlace(new WidgetPlacement(_placement.WidgetId, startCell), target))
            {
                var resized = _layout.Resize(_placement.WidgetId, col, row);
                _placement = resized;
                ApplyPlacementGeometry();
            }
        });

        PointerEventHandler up = default!;
        up = (s, e) =>
        {
            handle.ReleasePointerCapture(e.Pointer);
            handle.PointerMoved -= move;
            handle.PointerReleased -= up;
        };

        handle.PointerMoved += move;
        handle.PointerReleased += up;
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