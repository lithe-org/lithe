using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Lithe.Widgets;

/// <summary>
/// 组件卡片容器：真材质层（<see cref="SystemBackdropElement"/>）+ 色调描边层
/// + 内容槽。环境不支持真材质时自动回退半透明纯色，行为与旧版单层 Border 一致。
/// </summary>
/// <remarks>
/// 层级顺序即 Grid 子元素顺序：材质在最底，色调描边居中，内容由调用方最后
/// 添加到 <see cref="Root"/>（后添加即在上层）。编辑模式描边强调挂色调层，
/// 与旧实现挂点一致。
/// </remarks>
public sealed class WidgetCard
{
    /// <summary>
    /// 真材质生效时色调 alpha 的上限：深色高不透明度会糊死材质，
    /// 压到 0.35 以下既保留色调又让材质透出来。
    /// </summary>
    private const double MaterialTintCap = 0.35;

    private readonly Border _tint;
    private readonly SystemBackdropElement? _material;
    private WidgetBackgroundKind _appliedKind = WidgetBackgroundKind.Transparent;

    /// <summary>
    /// 创建卡片容器。
    /// </summary>
    /// <param name="kind">初始背景类型。</param>
    /// <param name="color">色调颜色。</param>
    /// <param name="opacity">色调不透明度 0~1。</param>
    public WidgetCard(WidgetBackgroundKind kind, Color color, double opacity)
    {
        Root = new Grid();

        _material = WidgetBackground.TryCreateMaterial();
        if (_material is not null)
        {
            Root.Children.Add(_material);
        }

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
    /// 应用外观：切换真材质显隐、材质类型与色调。
    /// </summary>
    public void Apply(WidgetBackgroundKind kind, Color color, double opacity)
    {
        var useMaterial = _material is not null
            && kind is WidgetBackgroundKind.Acrylic or WidgetBackgroundKind.Mica;

        if (_material is not null)
        {
            if (useMaterial)
            {
                // 只有类型变化才换材质对象，避免重复分配。
                if (_material.SystemBackdrop is null || _appliedKind != kind)
                {
                    _material.SystemBackdrop = kind == WidgetBackgroundKind.Mica
                        ? new MicaBackdrop { Kind = MicaKind.Base }
                        : new DesktopAcrylicBackdrop();
                }

                _material.Visibility = Visibility.Visible;
            }
            else
            {
                _material.Visibility = Visibility.Collapsed;
            }
        }

        _tint.Background = useMaterial
            ? WidgetBackground.Create(WidgetBackgroundKind.Solid, color, Math.Min(opacity, MaterialTintCap))
            : WidgetBackground.Create(kind, color, opacity);

        _appliedKind = kind;
    }

    /// <summary>
    /// 编辑模式描边强调，挂在色调层（旧实现挂 Border 容器，视觉等价）。
    /// </summary>
    public void ApplyEditBorder(bool editing)
    {
        _tint.BorderBrush = new SolidColorBrush(Colors.White with { A = editing ? (byte)0xAA : (byte)0x33 });
    }
}
