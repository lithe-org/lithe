using Lithe.Core.Layout;
using Microsoft.UI.Xaml;

namespace Lithe.Widgets;

/// <summary>
/// 组件视图工厂。顶栏与桌面分别创建各自的视图。
/// </summary>
public interface IWidgetFactory
{
    /// <summary>
    /// 组件元数据。
    /// </summary>
    IWidget Definition { get; }

    /// <summary>
    /// 创建视图。
    /// </summary>
    /// <param name="mode">呈现位置。</param>
    /// <param name="context">运行上下文。</param>
    FrameworkElement CreateView(WidgetViewMode mode, WidgetContext context);
}

/// <summary>
/// 响应式断点。组件按自身实际像素尺寸切换紧凑度。
/// </summary>
public enum WidgetSizeClass
{
    /// <summary>
    /// 紧凑：只显示最核心的信息。
    /// </summary>
    Compact,

    /// <summary>
    /// 常规：核心信息加次要信息。
    /// </summary>
    Regular,

    /// <summary>
    /// 展开：完整信息。
    /// </summary>
    Expanded,
}

/// <summary>
/// 组件视图可感知的宿主能力，由 <see cref="WidgetHost"/> 在断点变化时回调。
/// </summary>
public interface IWidgetViewAware
{
    /// <summary>
    /// 断点变化。
    /// </summary>
    void OnSizeClassChanged(WidgetSizeClass sizeClass);
}