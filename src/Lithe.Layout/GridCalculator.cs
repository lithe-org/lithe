using Lithe.Core.Layout;

namespace Lithe.Layout;

/// <summary>
/// 响应式网格计算：以分辨率为主档、物理尺寸为辅。
/// </summary>
/// <remarks>
/// 分档规则（见 Design.md 第 10.4 节）：1920×1080 用小单元、2560×1440 用中单元、
/// 3840×2160 用大单元；档位之间线性插值，保证任意分辨率都能得到整数行列。
/// 用户可以在设置里手动覆盖单元尺寸。
/// </remarks>
public sealed class GridCalculator : IGridCalculator
{
    /// <summary>
    /// 小单元基准：1920×1080 下的单元宽度。
    /// </summary>
    public const double SmallCellWidth = 80;

    /// <summary>
    /// 大单元基准：3840×2160 下的单元宽度。高分辨率下单元更大更好点。
    /// </summary>
    public const double LargeCellWidth = 112;

    /// <summary>
    /// 单元默认宽高比（宽 / 高）。
    /// </summary>
    public const double DefaultCellRatio = 2.2;

    private readonly double _spacing;

    /// <summary>
    /// 创建网格计算器。
    /// </summary>
    /// <param name="spacing">单元之间的间距，全局统一，默认 8px。</param>
    public GridCalculator(double spacing = 8)
    {
        if (spacing < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing));
        }

        _spacing = spacing;
    }

    /// <summary>
    /// 手动覆盖单元宽度。传入 0 或负数表示恢复自动分档。
    /// </summary>
    public double CellWidthOverride { get; set; }

    /// <inheritdoc />
    public GridMetrics Calculate(DisplayMetrics display)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(display.WidthPixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(display.HeightPixels);

        var width = CellWidthOverride > 0 ? CellWidthOverride : ResolveCellWidth(display.WidthPixels);
        var height = width / DefaultCellRatio;

        // 每个单元实际可用宽度 = 单元宽度 - 间距，保证最后一行也能整齐收口。
        var columns = Math.Max(1, (int)Math.Floor((display.WidthPixels + _spacing) / (width + _spacing)));
        var rows = Math.Max(1, (int)Math.Floor((display.HeightPixels + _spacing) / (height + _spacing)));

        return new GridMetrics(columns, rows, width, height, _spacing, 0, 0);
    }

    /// <summary>
    /// 把网格单元换算成像素矩形，间距在单元之外。
    /// </summary>
    public static PixelRect ToPixels(GridMetrics metrics, GridCell cell)
    {
        var left = metrics.OriginLeft + (cell.Column * (metrics.CellWidth + metrics.Spacing));
        var top = metrics.OriginTop + (cell.Row * (metrics.CellHeight + metrics.Spacing));
        var width = (cell.ColSpan * metrics.CellWidth) + ((cell.ColSpan - 1) * metrics.Spacing);
        var height = (cell.RowSpan * metrics.CellHeight) + ((cell.RowSpan - 1) * metrics.Spacing);
        return new PixelRect(left, top, width, height);
    }

    private static double ResolveCellWidth(int displayWidth)
    {
        // 以 1920 与 3840 两档为锚点线性插值，落在两档之间时平滑过渡。
        var lowWidth = 1920;
        var highWidth = 3840;

        if (displayWidth <= lowWidth)
        {
            return SmallCellWidth;
        }

        if (displayWidth >= highWidth)
        {
            return LargeCellWidth;
        }

        var ratio = (double)(displayWidth - lowWidth) / (highWidth - lowWidth);
        return SmallCellWidth + ((LargeCellWidth - SmallCellWidth) * ratio);
    }
}