using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lithe_App.Shell;
using Lithe.Widgets.Builtin;

namespace Lithe_App.ViewModels;

/// <summary>
/// 桌面层控制面板的 ViewModel。里程碑 1-2 期间作为临时控制台，
/// 设置窗口接入后由设置界面接管同样的开关。
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    public MainPageViewModel()
    {
        RefreshStatus();
    }

    /// <summary>
    /// 桌面层当前状态描述。
    /// </summary>
    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>
    /// 切换桌面层的鼠标穿透。
    /// </summary>
    [RelayCommand]
    private void ToggleClickThrough()
    {
        DesktopHost.Instance?.ToggleClickThrough();
        RefreshStatus();
    }

    [RelayCommand]
    private void AddClock()
    {
        StatusText = DesktopHost.Instance?.AddWidget(ClockWidget.WidgetId) == true
            ? "已添加时钟组件"
            : "添加时钟失败";
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        App.EditMode.Toggle();
        RefreshStatus();
    }

    [RelayCommand]
    private void AttachToWorkerW()
    {
        if (DesktopHost.Instance is not { } host)
        {
            StatusText = "桌面层尚未创建。";
            return;
        }

        StatusText = "正在查找桌面宿主…";
        _ = ApplyAttachResultAsync(host);
    }

    [RelayCommand]
    private void DetachFromWorkerW()
    {
        StatusText = DesktopHost.Instance?.DetachFromDesktopLayer() ?? "桌面层尚未创建。";
    }

    [RelayCommand]
    private void RefreshStatus()
    {
        StatusText = DesktopHost.Instance?.DescribeState() ?? "桌面层尚未创建。";
    }

    private async Task ApplyAttachResultAsync(DesktopHost host)
    {
        StatusText = await host.AttachToDesktopLayerAsync();
    }
}