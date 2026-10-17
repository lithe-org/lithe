using Lithe.Core.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Lithe.Widgets;

/// <summary>
/// 组件卡片容器：圆角色调层 + 内容槽。
/// </summary>
/// <remarks>
/// 无窗口级材质：卡片色调按配置原值绘制，半透明纯色靠窗口逐像素透明
/// 合成，确定可见；组件之外的窗口空白区域透出壁纸。窗口级 acrylic 与
/// "空隙透出壁纸"的设计冲突（组件拖开后盒内空隙被填充成 acrylic 亮块，
/// 2026-10-17 截图实测），页内 SystemBackdropElement 在同形态窗口采不到
/// 壁纸；真亚克力待每组件独立窗口架构（M3）。编辑模式描边强调挂色调层。
/// </remarks>
public sealed class WidgetCard
{
    private readonly string _widgetId;
    private readonly Border _tint;

    /// <summary>
    /// 创建卡片容器。
    /// </summary>
    /// <param name="widgetId">组件 Id，用于诊断日志。</param>
    /// <param name="kind">初始背景类型。</param>
    /// <param name="color">色调颜色。</param>
    /// <param name="opacity">色调不透明度 0~1。</param>
    public WidgetCard(string widgetId, WidgetBackgroundKind kind, Color color, double opacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetId);

        _widgetId = widgetId;
        Root = new Grid();

        _tint = new Border
        {
            CornerRadius = new CornerRadius(WidgetBackground.CornerRadius),
            BorderThickness = new Thickness(1),
        };
        Root.Children.Add(_tint);

        Apply(kind, color, opacity);
        ApplyEditBorder(false);
    }

    /// <summary>
    /// 卡片根元素。内容层由调用方添加到此后即在最上层。
    /// </summary>
    public Grid Root { get; }

    /// <summary>
    /// 应用外观：按配置刷新色调画刷。
    /// </summary>
    public void Apply(WidgetBackgroundKind kind, Color color, double opacity)
    {
        _tint.Background = WidgetBackground.Create(kind, color, opacity);

        // 渲染级日志：配置值与实际落地色值，排查样式问题直接看 shell.log。
        var brush = (SolidColorBrush)_tint.Background;
        var applied = brush.Color;
        AppLog.Info(
            $"卡片 {_widgetId} 外观：{kind} #{color.R:X2}{color.G:X2}{color.B:X2} {opacity:P0}" +
            $" → 色调 #{applied.R:X2}{applied.G:X2}{applied.B:X2} alpha {applied.A}");
    }

    /// <summary>
    /// 编辑模式描边强调，挂在色调层。
    /// </summary>
    /// <remarks>
    /// 非编辑态描边 alpha 压到 0x18：20% 以上的白边在深色磨砂卡片上
    /// 过亮，半透明圆角边缘还会合成出亮线（2026-10-17 截图实测）。
    /// </remarks>
    public void ApplyEditBorder(bool editing)
    {
        _tint.BorderBrush = new SolidColorBrush(Colors.White with { A = editing ? (byte)0xAA : (byte)0x18 });
    }
}
