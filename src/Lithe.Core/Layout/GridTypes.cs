namespace Lithe.Core.Layout;

/// <summary>
/// 显示器信息。这里用纯 .NET 类型描述，避免布局层依赖 WinRT；
/// 由宿主层把 <c>DisplayArea</c> 转换过来。
/// </summary>
/// <param name="WidthPixels">可用宽度（像素）。</param>
/// <param name="HeightPixels">可用高度（像素）。</param>
/// <param name="Scale">缩放比例，1.0 表示 100%。</param>
/// <param name="IsPrimary">是否主显示器。</param>
public readonly record struct DisplayMetrics(int WidthPixels, int HeightPixels, double Scale = 1.0, bool IsPrimary = true);

/// <summary>
/// 网格指标，一块显示器对应一套。
/// </summary>
/// <param name="Columns">列数。</param>
/// <param name="Rows">行数。</param>
/// <param name="CellWidth">单元宽度（像素，含间距）。</param>
/// <param name="CellHeight">单元高度（像素，含间距）。</param>
/// <param name="Spacing">单元之间的间距（像素）。</param>
/// <param name="OriginLeft">网格左边界（像素）。</param>
/// <param name="OriginTop">网格上边界（像素）。</param>
public readonly record struct GridMetrics(
    int Columns,
    int Rows,
    double CellWidth,
    double CellHeight,
    double Spacing,
    double OriginLeft,
    double OriginTop)
{
    /// <summary>
    /// 网格总宽度（像素）。
    /// </summary>
    public double TotalWidth => Columns * CellWidth;

    /// <summary>
    /// 网格总高度（像素）。
    /// </summary>
    public double TotalHeight => Rows * CellHeight;
}

/// <summary>
/// 网格单元，组件占位用。
/// </summary>
/// <param name="Column">起始列。</param>
/// <param name="Row">起始行。</param>
/// <param name="ColSpan">列跨度。</param>
/// <param name="RowSpan">行跨度。</param>
public readonly record struct GridCell(int Column, int Row, int ColSpan = 1, int RowSpan = 1)
{
    /// <summary>
    /// 结束列（不含）。
    /// </summary>
    public int ColumnEnd => Column + ColSpan;

    /// <summary>
    /// 结束行（不含）。
    /// </summary>
    public int RowEnd => Row + RowSpan;

    /// <summary>
    /// 是否与另一个单元重叠。接触不算重叠。
    /// </summary>
    public bool Intersects(GridCell other) =>
        Column < other.ColumnEnd && other.Column < ColumnEnd
        && Row < other.RowEnd && other.Row < RowEnd;

    /// <summary>
    /// 是否完全落在 <paramref name="metrics"/> 范围内。
    /// </summary>
    public bool IsInside(GridMetrics metrics) =>
        Column >= 0 && Row >= 0
        && ColumnEnd <= metrics.Columns && RowEnd <= metrics.Rows;
}

/// <summary>
/// 组件占位。
/// </summary>
/// <param name="WidgetId">组件 Id。</param>
/// <param name="Cell">占用的网格单元。</param>
public readonly record struct WidgetPlacement(string WidgetId, GridCell Cell);

/// <summary>
/// 组件尺寸约束。
/// </summary>
/// <param name="DefaultColSpan">默认列跨度。</param>
/// <param name="DefaultRowSpan">默认行跨度。</param>
/// <param name="MinColSpan">最小列跨度。</param>
/// <param name="MinRowSpan">最小行跨度。</param>
/// <param name="MaxColSpan">最大列跨度。</param>
/// <param name="MaxRowSpan">最大行跨度。</param>
public readonly record struct WidgetSizeConstraint(
    int DefaultColSpan,
    int DefaultRowSpan,
    int MinColSpan = 1,
    int MinRowSpan = 1,
    int MaxColSpan = 8,
    int MaxRowSpan = 8)
{
    /// <summary>
    /// 默认占位。
    /// </summary>
    public GridCell DefaultCell => new(0, 0, DefaultColSpan, DefaultRowSpan);

    /// <summary>
    /// 把跨度钳制到约束范围内。
    /// </summary>
    public (int ColSpan, int RowSpan) Clamp(int colSpan, int rowSpan)
    {
        var col = Math.Clamp(colSpan, MinColSpan, Math.Max(MinColSpan, MaxColSpan));
        var row = Math.Clamp(rowSpan, MinRowSpan, Math.Max(MinRowSpan, MaxRowSpan));
        return (col, row);
    }
}

/// <summary>
/// 像素矩形。布局层不依赖 WinRT，所以自带矩形类型。
/// </summary>
/// <param name="Left">左边界。</param>
/// <param name="Top">上边界。</param>
/// <param name="Width">宽度。</param>
/// <param name="Height">高度。</param>
public readonly record struct PixelRect(double Left, double Top, double Width, double Height)
{
    /// <summary>
    /// 右边界。
    /// </summary>
    public double Right => Left + Width;

    /// <summary>
    /// 下边界。
    /// </summary>
    public double Bottom => Top + Height;
}

/// <summary>
/// 组件呈现位置。顶栏与桌面分别调用不同的视图工厂。
/// </summary>
public enum WidgetViewMode
{
    /// <summary>
    /// 顶栏。
    /// </summary>
    TopBar,

    /// <summary>
    /// 桌面。
    /// </summary>
    Desktop,
}

/// <summary>
/// 组件定义。框架侧的契约，具体实现在组件项目里。
/// </summary>
public interface IWidget
{
    /// <summary>
    /// 组件 Id，对应布局文件里的标识。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 显示名。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 尺寸约束。
    /// </summary>
    WidgetSizeConstraint SizeConstraint { get; }

    /// <summary>
    /// 依赖的数据源 Id。
    /// </summary>
    IReadOnlyList<string> DataSourceIds { get; }
}