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
/// 真材质由桌面层窗口的 <c>DesktopAcrylicBackdrop</c> 统一提供：WinUI 3 的
/// lifted Composition 无法直接采样桌面，页内 <c>SystemBackdropElement</c> 在
/// 逐像素透明 + owner 挂载窗口里实测只渲染系统 fallback 纯色（2026-10-17），
/// 故材质挂窗口、卡片用半透明色调压深。编辑模式描边强调挂色调层。
/// </remarks>
public sealed class WidgetCard
{
    /// <summary>
    /// 亚克力/云母卡片在窗口材质上的色调 alpha 上限：封顶压深，
    /// 避免高不透明度把窗口材质的磨砂感糊死。
    /// </summary>
    private const double MaterialTintCap = 0.6;

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
    /// 应用外观：计算色调 alpha 并刷新背景画刷。
    /// </summary>
    public void Apply(WidgetBackgroundKind kind, Color color, double opacity)
    {
        // 亚克力/云母的真实感来自窗口级材质，卡片色调只做压深，封顶处理；
        // 纯色/透明按配置原值绘制。
        var effective = kind is WidgetBackgroundKind.Acrylic or WidgetBackgroundKind.Mica
            ? Math.Min(opacity, MaterialTintCap)
            : opacity;

        _tint.Background = WidgetBackground.Create(kind, color, effective);

        // 渲染级日志：配置值经封顶后的实际落地值，用于排查样式问题。
        var brush = (SolidColorBrush)_tint.Background;
        var applied = brush.Color;
        AppLog.Info(
            $"卡片 {_widgetId} 外观：{kind} #{color.R:X2}{color.G:X2}{color.B:X2} {opacity:P0}" +
            $" → 色调 #{applied.R:X2}{applied.G:X2}{applied.B:X2} alpha {applied.A}（{effective:P0}）");
    }

    /// <summary>
    /// 编辑模式描边强调，挂在色调层。
    /// </summary>
    public void ApplyEditBorder(bool editing)
    {
        _tint.BorderBrush = new SolidColorBrush(Colors.White with { A = editing ? (byte)0xAA : (byte)0x33 });
    }
}
