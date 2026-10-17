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
    public static Brush Create(WidgetBackgroundKind kind, Color color, double opacity = 0.7)
    {
        var alpha = (byte)Math.Clamp(opacity * 255, 0, 255);

        return kind switch
        {
            WidgetBackgroundKind.Transparent => new SolidColorBrush(Colors.Transparent),
            WidgetBackgroundKind.Solid => new SolidColorBrush(color with { A = alpha }),
            WidgetBackgroundKind.Acrylic => new AcrylicBrush
            {
                TintColor = color,
                TintOpacity = opacity,
                FallbackColor = color with { A = alpha },
            },
            // 云母作用于窗口背景，组件层面用亚克力近似。
            WidgetBackgroundKind.Mica => new AcrylicBrush
            {
                TintColor = color,
                TintOpacity = Math.Min(1.0, opacity + 0.1),
                FallbackColor = color with { A = alpha },
            },
            _ => new AcrylicBrush { TintColor = color, TintOpacity = opacity, FallbackColor = color },
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