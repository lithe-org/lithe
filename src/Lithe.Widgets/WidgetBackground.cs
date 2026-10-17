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
    /// 创建色调画刷。
    /// </summary>
    /// <param name="kind">背景类型。</param>
    /// <param name="color">纯色或色调。</param>
    /// <param name="opacity">不透明度 0~1。</param>
    /// <remarks>
    /// 真材质由 <see cref="WidgetCard"/> 的 <see cref="SystemBackdropElement"/>
    /// 承担（SDK 2.0+），本方法只画色调/描边之下的纯色层。无 backdrop 的
    /// 窗口里旧版 AcrylicBrush 渲染为全透明（实测），半透明
    /// SolidColorBrush 靠窗口逐像素透明合成，确定可见，是不可用环境的兜底。
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
    /// 尝试创建页内真材质元素（Windows App SDK 2.0+ 的
    /// <see cref="SystemBackdropElement"/>）。环境不支持时返回
    /// <see langword="null"/>，调用方回退半透明纯色方案。
    /// </summary>
    public static SystemBackdropElement? TryCreateMaterial()
    {
        try
        {
            // 具体材质（亚克力/云母）由调用方按外观配置赋值 SystemBackdrop。
            return new SystemBackdropElement
            {
                CornerRadius = new CornerRadius(CornerRadius),
            };
        }
        catch (Exception)
        {
            // 缺 SDK 2.0 运行时等环境：类型加载或构造失败，走纯色回退。
            return null;
        }
    }
}