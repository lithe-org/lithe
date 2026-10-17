using Lithe.Core.Layout;

namespace Lithe.Layout;

/// <summary>
/// 布局引擎。维护组件占位表，负责放置、移动、缩放与碰撞检测。
/// </summary>
public sealed class LayoutEngine : ILayoutEngine
{
    private readonly Lock _gate = new();
    private readonly List<WidgetPlacement> _placements = [];
    private readonly Dictionary<string, WidgetSizeConstraint> _constraints = [];
    private GridMetrics _metrics;

    /// <summary>
    /// 创建布局引擎。
    /// </summary>
    /// <param name="metrics">初始显示器网格指标。</param>
    public LayoutEngine(GridMetrics metrics)
    {
        _metrics = metrics;
    }

    /// <inheritdoc />
    public GridMetrics Metrics
    {
        get
        {
            lock (_gate)
            {
                return _metrics;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? LayoutChanged;

    /// <summary>
    /// 注册组件及其尺寸约束，供缩放钳制使用。
    /// </summary>
    public void RegisterWidget(string widgetId, WidgetSizeConstraint constraint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetId);

        lock (_gate)
        {
            _constraints[widgetId] = constraint;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WidgetPlacement> GetAllPlacements()
    {
        lock (_gate)
        {
            return [.. _placements];
        }
    }

    /// <inheritdoc />
    public bool TryGetPlacement(string widgetId, out WidgetPlacement placement)
    {
        lock (_gate)
        {
            foreach (var item in _placements)
            {
                if (string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal))
                {
                    placement = item;
                    return true;
                }
            }
        }

        placement = default;
        return false;
    }

    /// <inheritdoc />
    public bool CanPlace(WidgetPlacement placement, GridCell target)
    {
        lock (_gate)
        {
            return CanPlaceCore(placement.WidgetId, target);
        }
    }

    /// <inheritdoc />
    public bool Place(string widgetId, GridCell cell)
    {
        lock (_gate)
        {
            if (!CanPlaceCore(widgetId, cell))
            {
                return false;
            }

            var clamped = ClampToConstraint(widgetId, cell);
            var placement = new WidgetPlacement(widgetId, clamped);
            var index = _placements.FindIndex(item => string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal));

            if (index >= 0)
            {
                _placements[index] = placement;
            }
            else
            {
                _placements.Add(placement);
            }
        }

        RaiseLayoutChanged();
        return true;
    }

    /// <inheritdoc />
    public WidgetPlacement Resize(string widgetId, int colSpan, int rowSpan)
    {
        WidgetPlacement result;

        lock (_gate)
        {
            var index = _placements.FindIndex(item => string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal));
            var cell = index >= 0 ? _placements[index].Cell : new GridCell(0, 0, colSpan, rowSpan);

            var (col, row) = _constraints.TryGetValue(widgetId, out var constraint)
                ? constraint.Clamp(colSpan, rowSpan)
                : (Math.Max(1, colSpan), Math.Max(1, rowSpan));

            var resized = new WidgetPlacement(widgetId, cell with { ColSpan = col, RowSpan = row });

            if (index >= 0)
            {
                _placements[index] = resized;
            }
            else
            {
                _placements.Add(resized);
            }

            result = resized;
        }

        RaiseLayoutChanged();
        return result;
    }

    /// <inheritdoc />
    public bool Remove(string widgetId)
    {
        bool removed;
        lock (_gate)
        {
            removed = _placements.RemoveAll(item => string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal)) > 0;
        }

        if (removed)
        {
            RaiseLayoutChanged();
        }

        return removed;
    }

    /// <summary>
    /// 切换显示器后重新计算网格。越界的组件会被拉回可见范围内。
    /// </summary>
    public void UpdateMetrics(GridMetrics metrics)
    {
        lock (_gate)
        {
            _metrics = metrics;

            for (var i = 0; i < _placements.Count; i++)
            {
                var placement = _placements[i];
                var cell = placement.Cell;

                var column = Math.Clamp(cell.Column, 0, Math.Max(0, metrics.Columns - 1));
                var row = Math.Clamp(cell.Row, 0, Math.Max(0, metrics.Rows - 1));
                var colSpan = Math.Clamp(cell.ColSpan, 1, Math.Max(1, metrics.Columns - column));
                var rowSpan = Math.Clamp(cell.RowSpan, 1, Math.Max(1, metrics.Rows - row));

                _placements[i] = placement with { Cell = new GridCell(column, row, colSpan, rowSpan) };
            }
        }

        RaiseLayoutChanged();
    }

    /// <summary>
    /// 从布局文件批量恢复占位，越界或重叠的条目会被丢弃。
    /// </summary>
    public int Load(IEnumerable<WidgetPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        var loaded = 0;
        lock (_gate)
        {
            _placements.Clear();
            foreach (var placement in placements)
            {
                if (CanPlaceCore(placement.WidgetId, placement.Cell))
                {
                    _placements.Add(placement);
                    loaded++;
                }
            }
        }

        RaiseLayoutChanged();
        return loaded;
    }

    private bool CanPlaceCore(string widgetId, GridCell target)
    {
        if (!target.IsInside(_metrics))
        {
            return false;
        }

        foreach (var item in _placements)
        {
            if (string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (item.Cell.Intersects(target))
            {
                return false;
            }
        }

        return true;
    }

    private GridCell ClampToConstraint(string widgetId, GridCell cell)
    {
        if (!_constraints.TryGetValue(widgetId, out var constraint))
        {
            return cell with { ColSpan = Math.Max(1, cell.ColSpan), RowSpan = Math.Max(1, cell.RowSpan) };
        }

        var (col, row) = constraint.Clamp(cell.ColSpan, cell.RowSpan);
        return cell with { ColSpan = col, RowSpan = row };
    }

    private void RaiseLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);
}