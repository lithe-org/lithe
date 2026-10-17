using Lithe_App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Lithe_App;

/// <summary>
/// 桌面层控制面板。里程碑 1-2 期间的临时入口，后续由设置窗口接管。
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();

        Loaded += (_, _) => ViewModel.InitializePanel();
    }

    private void OnWidgetSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.FirstOrDefault() is string id)
        {
            ViewModel.SelectWidget(id);
        }
    }

    private void OnBackgroundSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.FirstOrDefault() is string kind)
        {
            ViewModel.SelectedBackground = kind;
        }
    }

    private void OnColorSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.FirstOrDefault() is string color)
        {
            ViewModel.SelectedColor = color;
        }
    }

    private void OnOpacityChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (sender is Slider slider)
        {
            ViewModel.Opacity = slider.Value;
        }
    }
}