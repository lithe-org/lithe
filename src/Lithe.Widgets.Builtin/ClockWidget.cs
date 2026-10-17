using CommunityToolkit.Mvvm.ComponentModel;
using Lithe.Core.Data;
using Lithe.Core.Diagnostics;
using Lithe.Core.Layout;
using Lithe.Data.Clock;
using Lithe.Widgets;
using Microsoft.UI.Xaml;

namespace Lithe.Widgets.Builtin;

/// <summary>
/// 时钟组件。默认 2×1，Compact 只显示时分，Expanded 显示大字体加日期与星期。
/// </summary>
public sealed class ClockWidget : IWidgetFactory
{
    /// <summary>
    /// 组件 Id。
    /// </summary>
    public const string WidgetId = "clock";

    private readonly ClockDefinition _definition = new();

    /// <inheritdoc />
    public IWidget Definition => _definition;

    /// <inheritdoc />
    public FrameworkElement CreateView(WidgetViewMode mode, WidgetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var viewModel = new ClockWidgetViewModel(context);
        viewModel.LoadSettings();
        return new ClockWidgetView(viewModel);
    }

    private sealed class ClockDefinition : IWidget
    {
        public string Id => WidgetId;

        public string DisplayName => "时钟";

        public WidgetSizeConstraint SizeConstraint { get; } = new(DefaultColSpan: 2, DefaultRowSpan: 1, MinColSpan: 1, MinRowSpan: 1, MaxColSpan: 4, MaxRowSpan: 2);

        public IReadOnlyList<string> DataSourceIds { get; } = [Lithe.Data.Clock.ClockDataSource.SourceId];
    }
}

/// <summary>
/// 时钟组件 ViewModel。数据来自 <see cref="ClockDataSource"/>，更新统一切回 UI 线程。
/// </summary>
public sealed partial class ClockWidgetViewModel : ObservableObject, IDisposable, IWidgetViewAware
{
    private const double CompactFontSize = 22;
    private const double RegularFontSize = 30;
    private const double ExpandedFontSize = 44;

    private readonly WidgetContext _context;
    private readonly ClockDataSource? _clock;
    private readonly Timer? _fallbackTimer;
    private WidgetSizeClass _sizeClass = WidgetSizeClass.Regular;

    [ObservableProperty]
    public partial string TimeText { get; set; } = "--:--";

    [ObservableProperty]
    public partial string DateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double TimeFontSize { get; set; } = RegularFontSize;

    [ObservableProperty]
    public partial bool ShowDate { get; set; } = true;

    /// <summary>
    /// 创建时钟 ViewModel。
    /// </summary>
    public ClockWidgetViewModel(WidgetContext context)
    {
        _context = context;

        _clock = context.GetDataSource<ClockDataSource>(Lithe.Data.Clock.ClockDataSource.SourceId);
        if (_clock is null)
        {
            // 没有注册时钟数据源时退化为本地定时器，保证组件仍然可用。
            AppLog.Warn("时钟组件未找到时钟数据源，已退化为本地定时器");
            _fallbackTimer = new Timer(_ => Apply(DateTimeOffset.Now), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
        else
        {
            _clock.Updated += OnClockUpdated;
            Apply(_clock.CurrentValue.Now);
        }
    }

    /// <summary>
    /// 读取配置项并写入 ViewModel。
    /// </summary>
    public void LoadSettings()
    {
        Use24Hour = _context.GetSetting(ClockWidget.WidgetId, "use24hour", true);
        ShowSeconds = _context.GetSetting(ClockWidget.WidgetId, "show_seconds", false);
        ShowDate = _context.GetSetting(ClockWidget.WidgetId, "show_date", true);
        ShowWeekday = _context.GetSetting(ClockWidget.WidgetId, "show_weekday", false);
        BaseFontSize = _context.GetSetting(ClockWidget.WidgetId, "font_size", 0.0);
        ApplyFontSize();
    }

    /// <summary>
    /// 是否使用 24 小时制。
    /// </summary>
    public bool Use24Hour { get; private set; } = true;

    /// <summary>
    /// 是否显示秒。默认关闭，避免不必要的视觉跳动。
    /// </summary>
    public bool ShowSeconds { get; private set; }

    /// <summary>
    /// 是否显示星期。
    /// </summary>
    public bool ShowWeekday { get; private set; }

    /// <summary>
    /// 用户手动指定的字号，0 表示按断点自动。
    /// </summary>
    public double BaseFontSize { get; private set; }

    /// <inheritdoc />
    public void OnSizeClassChanged(WidgetSizeClass sizeClass)
    {
        _sizeClass = sizeClass;

        ShowDate = sizeClass >= WidgetSizeClass.Regular && _context.GetSetting(ClockWidget.WidgetId, "show_date", true);
        ShowSeconds = sizeClass == WidgetSizeClass.Expanded && _context.GetSetting(ClockWidget.WidgetId, "show_seconds", false);
        ShowWeekday = sizeClass == WidgetSizeClass.Expanded && _context.GetSetting(ClockWidget.WidgetId, "show_weekday", false);

        ApplyFontSize();
        OnPropertyChanged(nameof(ShowSeconds));
        OnPropertyChanged(nameof(ShowWeekday));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_clock is not null)
        {
            _clock.Updated -= OnClockUpdated;
        }

        _fallbackTimer?.Dispose();
    }

    private void OnClockUpdated(object? sender, DataSourceUpdatedEventArgs<ClockSnapshot> args)
    {
        // 数据源在后台线程推送，更新 UI 必须切回 UI 线程。
        _context.RunOnUiThread(() => Apply(args.Value.Now));
    }

    private void Apply(DateTimeOffset now)
    {
        TimeText = ClockDataSource.FormatTime(now, Use24Hour, ShowSeconds);
        DateText = ClockDataSource.FormatDate(now, ShowWeekday);
    }

    private void ApplyFontSize()
    {
        if (BaseFontSize > 0)
        {
            TimeFontSize = BaseFontSize;
            return;
        }

        TimeFontSize = _sizeClass switch
        {
            WidgetSizeClass.Compact => CompactFontSize,
            WidgetSizeClass.Expanded => ExpandedFontSize,
            _ => RegularFontSize,
        };
    }
}