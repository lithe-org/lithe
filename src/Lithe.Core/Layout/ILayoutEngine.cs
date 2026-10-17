namespace Lithe.Core.Layout;

/// <summary>
/// 响应式网格计算。不同分辨率用不同档位的单元尺寸。
/// </summary>
public interface IGridCalculator
{
    /// <summary>
    /// 按显示器尺寸计算网格指标。
    /// </summary>
    /// <param name="display">显示器信息。</param>
    GridMetrics Calculate(DisplayMetrics display);
}

/// <summary>
/// 布局引擎：放置、移动、缩放组件，并做碰撞检测。
/// </summary>
public interface ILayoutEngine
{
    /// <summary>
    /// 当前显示器对应的网格指标。
    /// </summary>
    GridMetrics Metrics { get; }

    /// <summary>
    /// 当前所有占位。
    /// </summary>
    IReadOnlyList<WidgetPlacement> GetAllPlacements();

    /// <summary>
    /// 取某个组件的占位。
    /// </summary>
    bool TryGetPlacement(string widgetId, out WidgetPlacement placement);

    /// <summary>
    /// 判断某个单元能否放置，忽略越界与重叠。
    /// </summary>
    bool CanPlace(WidgetPlacement placement, GridCell target);

    /// <summary>
    /// 放置或移动组件，越界或重叠时返回 <see langword="false"/>。
    /// </summary>
    bool Place(string widgetId, GridCell cell);

    /// <summary>
    /// 缩放组件，跨度会被钳制到组件约束内。
    /// </summary>
    WidgetPlacement Resize(string widgetId, int colSpan, int rowSpan);

    /// <summary>
    /// 移除组件。
    /// </summary>
    bool Remove(string widgetId);

    /// <summary>
    /// 布局发生变化（放置、缩放、移除、换显示器）。
    /// </summary>
    event EventHandler? LayoutChanged;
}

/// <summary>
/// 编辑模式：允许拖拽、缩放、删除组件的全局状态。
/// </summary>
public interface IEditModeService
{
    /// <summary>
    /// 是否处于编辑模式。
    /// </summary>
    bool IsEditing { get; }

    /// <summary>
    /// 进入编辑模式。
    /// </summary>
    void Enter();

    /// <summary>
    /// 退出编辑模式。
    /// </summary>
    void Exit();

    /// <summary>
    /// 切换编辑模式。
    /// </summary>
    void Toggle();

    /// <summary>
    /// 编辑模式状态变化。
    /// </summary>
    event EventHandler? EditModeChanged;
}