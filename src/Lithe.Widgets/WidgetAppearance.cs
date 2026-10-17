using System.Globalization;
using Lithe.Core.Config;
using Microsoft.UI;
using Windows.UI;

namespace Lithe.Widgets;

/// <summary>
/// 组件外观配置。写到 <c>config.toml</c> 的 <c>widgets.&lt;id&gt;</c> 段下。
/// </summary>
/// <param name="Background">背景类型。</param>
/// <param name="Color">颜色，十六进制字符串。</param>
/// <param name="Opacity">不透明度 0~1。</param>
public readonly record struct WidgetAppearance(WidgetBackgroundKind Background, string ColorHex, double Opacity)
{
    /// <summary>
    /// 默认外观：亚克力、深色、70% 不透明度。
    /// </summary>
    public static WidgetAppearance Default { get; } = new(WidgetBackgroundKind.Acrylic, "#202020", 0.7);

    /// <summary>
    /// 解析后的颜色。
    /// </summary>
    public Color ResolveColor() => HexToColor(ColorHex);

    /// <summary>
    /// 十六进制颜色转 <see cref="Color"/>。
    /// </summary>
    public static Color HexToColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Colors.Black;
        }

        var text = hex.Trim().TrimStart('#');
        if (text.Length == 6)
        {
            text += "FF";
        }

        return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? Color.FromArgb(
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF))
            : Colors.Black;
    }

    /// <summary>
    /// <see cref="Color"/> 转十六进制字符串（不带 <c>#</c>）。
    /// </summary>
    public static string ToHex(Color color) =>
        $"{color.R:X2}{color.G:X2}{color.B:X2}";
}

/// <summary>
/// 组件外观读写。只走配置服务，保存由配置的 500ms 防抖统一处理。
/// </summary>
public static class WidgetAppearanceService
{
    /// <summary>
    /// 读取组件外观，缺失项用默认值补齐。
    /// </summary>
    public static WidgetAppearance Load(IConfigService? config, string widgetId)
    {
        var background = config?.GetOrDefault($"widgets.{widgetId}.background", WidgetAppearance.Default.Background.ToString()) ?? WidgetAppearance.Default.Background.ToString();

        return new WidgetAppearance(
            Enum.TryParse<WidgetBackgroundKind>(background, ignoreCase: true, out var kind) ? kind : WidgetAppearance.Default.Background,
            config?.GetOrDefault($"widgets.{widgetId}.background_color", WidgetAppearance.Default.ColorHex) ?? WidgetAppearance.Default.ColorHex,
            config?.GetOrDefault($"widgets.{widgetId}.background_opacity", WidgetAppearance.Default.Opacity) ?? WidgetAppearance.Default.Opacity);
    }

    /// <summary>
    /// 保存组件外观。
    /// </summary>
    public static void Save(IConfigService? config, string widgetId, WidgetAppearance appearance)
    {
        if (config is null)
        {
            return;
        }

        config.Set($"widgets.{widgetId}.background", appearance.Background.ToString());
        config.Set($"widgets.{widgetId}.background_color", appearance.ColorHex);
        config.Set($"widgets.{widgetId}.background_opacity", appearance.Opacity);
    }
}