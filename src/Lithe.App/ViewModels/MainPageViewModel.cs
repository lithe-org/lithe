using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lithe.Widgets;
using Lithe_App.Shell;

namespace Lithe_App.ViewModels;

/// <summary>
/// 桌面层控制面板的 ViewModel。里程碑 1-2 期间作为临时控制台，
/// 设置窗口接入后由设置界面接管同样的开关。
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    /// <summary>
    /// 可选背景色。
    /// </summary>
    private static readonly (string Name, string Hex)[] s_colorPresets =
    [
        ("深色", "#202020"),
        ("墨绿", "#1B2B20"),
        ("藏蓝", "#16233A"),
        ("浅灰", "#E6E6E6"),
    ];

    private static readonly WidgetBackgroundKind[] s_backgroundKinds =
    [
        WidgetBackgroundKind.Acrylic,
        WidgetBackgroundKind.Transparent,
        WidgetBackgroundKind.Solid,
        WidgetBackgroundKind.Mica,
    ];

    public MainPageViewModel()
    {
        RefreshStatus();
    }

    /// <summary>
    /// 面板加载后同步组件列表与当前外观。
    /// </summary>
    public void InitializePanel()
    {
        RefreshWidgetList();
        SelectWidget(SelectedWidgetId ?? WidgetIds.FirstOrDefault());
    }

    /// <summary>
    /// 选中组件并读出它的外观设置。
    /// </summary>
    public void SelectWidget(string? widgetId)
    {
        SelectedWidgetId = widgetId;

        if (string.IsNullOrEmpty(widgetId))
        {
            return;
        }

        var appearance = WidgetAppearanceService.Load(App.Configuration, widgetId);
        SelectedBackground = appearance.Background.ToString();
        SelectedColor = s_colorPresets.FirstOrDefault(preset => preset.Hex == appearance.ColorHex).Name ?? s_colorPresets[0].Name;
        Opacity = appearance.Opacity;
    }

    /// <summary>
    /// 桌面层当前状态描述。
    /// </summary>
    [ObservableProperty]
    public partial string StatusText { get; set; }

    /// <summary>
    /// 桌面上的组件列表。
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> WidgetIds { get; set; } = [];

    /// <summary>
    /// 当前选中的组件。
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedWidgetId { get; set; }

    /// <summary>
    /// 背景类型选项。
    /// </summary>
    public IReadOnlyList<string> BackgroundOptions { get; } =
        [.. s_backgroundKinds.Select(static kind => kind.ToString())];

    /// <summary>
    /// 当前选中的背景类型。
    /// </summary>
    [ObservableProperty]
    public partial string SelectedBackground { get; set; } = nameof(WidgetBackgroundKind.Acrylic);

    /// <summary>
    /// 颜色预设名称。
    /// </summary>
    public IReadOnlyList<string> ColorOptions { get; } = [.. s_colorPresets.Select(static preset => preset.Name)];

    /// <summary>
    /// 当前选中的颜色名。
    /// </summary>
    [ObservableProperty]
    public partial string SelectedColor { get; set; } = s_colorPresets[0].Name;

    /// <summary>
    /// 背景不透明度。
    /// </summary>
    [ObservableProperty]
    public partial double Opacity { get; set; } = WidgetAppearance.Default.Opacity;

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
        StatusText = DesktopHost.Instance?.AddWidget(Lithe.Widgets.Builtin.ClockWidget.WidgetId) == true
            ? "已添加时钟组件"
            : "添加时钟失败";
        RefreshWidgetList();
    }

    [RelayCommand]
    private void AddTodo()
    {
        StatusText = DesktopHost.Instance?.AddWidget(Lithe.Widgets.Builtin.TodoWidget.WidgetId) == true
            ? "已添加待办组件"
            : "添加待办失败";
        RefreshWidgetList();
    }

    [RelayCommand]
    private void RemoveSelectedWidget()
    {
        if (SelectedWidgetId is not { } id)
        {
            return;
        }

        StatusText = DesktopHost.Instance?.RemoveWidget(id) == true
            ? $"已删除 {id}"
            : $"删除 {id} 失败";

        RefreshWidgetList();
        RefreshStatus();
    }

    /// <summary>
    /// 切换桌面编辑模式。
    /// </summary>
    [RelayCommand]
    private void ToggleEditMode()
    {
        App.EditMode.Toggle();
        RefreshStatus();
    }

    /// <summary>
    /// 保存当前外观设置到配置并立即应用。
    /// </summary>
    [RelayCommand]
    private void ApplyAppearance()
    {
        if (SelectedWidgetId is not { } id)
        {
            StatusText = "请先选择组件";
            return;
        }

        if (!Enum.TryParse<WidgetBackgroundKind>(SelectedBackground, out var kind))
        {
            StatusText = "背景类型无效";
            return;
        }

        var hex = s_colorPresets.FirstOrDefault(preset => preset.Name == SelectedColor).Hex ?? s_colorPresets[0].Hex;
        var appearance = new WidgetAppearance(kind, hex, Opacity);

        WidgetAppearanceService.Save(App.Configuration, id, appearance);

        StatusText = DesktopHost.Instance?.ApplyAppearance(id, appearance) == true
            ? $"已应用 {id} 的外观"
            : $"组件 {id} 不在桌面上";
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

    private void RefreshWidgetList()
    {
        WidgetIds = [.. App.Layout.GetAllPlacements().Select(static placement => placement.WidgetId)];
        SelectedWidgetId ??= WidgetIds.FirstOrDefault();
    }

    private async Task ApplyAttachResultAsync(DesktopHost host)
    {
        StatusText = await host.AttachToDesktopLayerAsync();
    }
}