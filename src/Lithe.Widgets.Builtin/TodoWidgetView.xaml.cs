using Lithe.Data.Todo;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Lithe.Widgets.Builtin;

/// <summary>
/// 待办组件视图。
/// </summary>
public sealed partial class TodoWidgetView : UserControl
{
    /// <summary>
    /// 创建待办视图。
    /// </summary>
    public TodoWidgetView(TodoWidgetViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// 视图模型。
    /// </summary>
    public TodoWidgetViewModel ViewModel { get; }

    private void OnItemClick(object sender, RoutedEventArgs args)
    {
        if (FindItem(sender) is { } item)
        {
            ViewModel.Toggle(item);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs args)
    {
        if (FindItem(sender) is { } item)
        {
            ViewModel.Remove(item);
        }
    }

    private void OnNewTitleKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.Add();
            args.Handled = true;
        }
    }

    private static TodoItem? FindItem(object sender)
    {
        var element = sender as FrameworkElement;
        return element?.DataContext as TodoItem;
    }
}