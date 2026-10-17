using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lithe_App.ViewModels;

/// <summary>
/// 主页面 ViewModel。
/// 使用 <c>partial property</c> 而非字段 + <see cref="ObservableProperty"/>，
/// 以保证 CsWinRT 生成器能产出 AOT 兼容的 WinRT 封送代码。
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Greeting { get; set; }

    [ObservableProperty]
    public partial int Counter { get; set; }

    public MainPageViewModel()
    {
        Greeting = "Hello, WinUI!";
    }

    [RelayCommand]
    private void Increment()
    {
        Counter++;
    }

    [RelayCommand]
    private void Decrement()
    {
        Counter--;
    }
}