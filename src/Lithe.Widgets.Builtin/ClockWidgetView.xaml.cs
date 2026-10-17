using Lithe.Widgets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lithe.Widgets.Builtin;

/// <summary>
/// 时钟组件视图。
/// </summary>
public sealed partial class ClockWidgetView : UserControl
{
    /// <summary>
    /// 创建时钟视图。
    /// </summary>
    public ClockWidgetView(ClockWidgetViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// 视图模型。
    /// </summary>
    public ClockWidgetViewModel ViewModel { get; }
}