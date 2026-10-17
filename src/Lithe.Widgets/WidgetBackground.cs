using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Lithe.Widgets;

/// <summary>
/// 组件背景类型。每个组件独立配置，默认亚克力。
/// </summary>
public enum WidgetBackgroundKind
{
    /// <summary>
    /// 透明，不画背景。
    /// </summary>
    Transparent,

    /// <summary>
    /// 亚克力。
    /// </summary>
    Acrylic,

    /// <summary>
    /// 纯色。
    /// </summary>
    Solid,

    /// <summary>
    /// 云母。云母要求窗口层级配合，不可用时回退亚克力。
    /// </summary>
    Mica,
}

/// <summary>
/// 组件背景工厂。
/// </summary>
public static class WidgetBackground
{
    /// <summary>
    /// 默认圆角（组件与通知卡片统一 12px）。
    /// </summary>
    public const double CornerRadius = 12;

    /// <summary>
    /// 创建背景画刷。
    /// </summary>
    /// <param name="kind">背景类型。</param>
    /// <param name="color">纯色或色调。</param>
    /// <param name="opacity">不透明度 0~1。</param>
    /// <remarks>
    /// 亚克力/云母降级为半透明纯色：桌面层窗口不设 SystemBackdrop（无透明
    /// backdrop 可用），实测 AcrylicBrush 在这种窗口里渲染为全透明，组件
    /// 只剩浮字。半透明 SolidColorBrush 靠窗口的逐像素透明合成，确定可见。
    /// 后续若给窗口加 DesktopAcrylicBackdrop，可改回真亚克力。
    /// </remarks>
    public static Brush Create(WidgetBackgroundKind kind, Color color, double opacity = 0.7)
    {
        var alpha = (byte)Math.Clamp(opacity * 255, 0, 255);

        return kind switch
        {
            WidgetBackgroundKind.Transparent => new SolidColorBrush(Colors.Transparent),
            WidgetBackgroundKind.Solid => new SolidColorBrush(color with { A = alpha }),
            WidgetBackgroundKind.Acrylic => new SolidColorBrush(color with { A = alpha }),
            // 云母作用于窗口背景，组件层面用更高不透明度的纯色近似。
            WidgetBackgroundKind.Mica => new SolidColorBrush(color with { A = (byte)Math.Clamp((opacity + 0.1) * 255, 0, 255) }),
            _ => new SolidColorBrush(color with { A = alpha }),
        };
    }

    /// <summary>
    /// 创建带统一圆角与描边的组件容器。
    /// </summary>
    public static Border CreateContainer(WidgetBackgroundKind kind, Color color, double opacity = 0.7)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(CornerRadius),
            Background = Create(kind, color, opacity),
            BorderBrush = new SolidColorBrush(Colors.White with { A = 0x33 }),
            BorderThickness = new Thickness(1),
        };
    }
}