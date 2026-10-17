using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lithe_App.Shell;

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
    private void AttachToWorkerW()
    {
        if (DesktopHost.Instance is not { } host)
        {
            StatusText = "桌面层尚未创建。";
            return;
        }

        StatusText = "正在查找 WorkerW…";
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