using System.Globalization;
using Lithe.Core.Data;

namespace Lithe.Data.Clock;

/// <summary>
/// 时间数据源，默认每秒推送一次。
/// </summary>
/// <remarks>
/// 刷新率按数据源配置，而不是按组件实例；顶栏与桌面上的时钟组件共享同一实例。
/// </remarks>
public sealed class ClockDataSource : IDataSource<ClockSnapshot>
{
    /// <summary>
    /// 数据源 Id。
    /// </summary>
    public const string SourceId = "clock";

    private static readonly TimeSpan s_minimumInterval = TimeSpan.FromMilliseconds(200);

    private readonly Lock _gate = new();
    private Timer? _timer;
    private ClockSnapshot _current;
    private TimeSpan _refreshInterval = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public string Id => SourceId;

    /// <inheritdoc />
    public ClockSnapshot CurrentValue
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public DateTime LastUpdated { get; private set; } = DateTime.MinValue;

    /// <inheritdoc />
    public TimeSpan RefreshInterval
    {
        get
        {
            lock (_gate)
            {
                return _refreshInterval;
            }
        }

        set
        {
            var interval = value < s_minimumInterval ? s_minimumInterval : value;

            lock (_gate)
            {
                if (interval == _refreshInterval)
                {
                    return;
                }

                _refreshInterval = interval;
                _timer?.Change(interval, interval);
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<DataSourceUpdatedEventArgs<ClockSnapshot>>? Updated;

    /// <inheritdoc />
    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_timer is not null)
            {
                return Task.CompletedTask;
            }

            _timer = new Timer(_ => Tick(), null, _refreshInterval, _refreshInterval);
        }

        Tick();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }

        return Task.CompletedTask;
    }

    private void Tick()
    {
        var now = DateTimeOffset.Now;

        ClockSnapshot snapshot;
        lock (_gate)
        {
            _current = new ClockSnapshot(now);
            snapshot = _current;
        }

        LastUpdated = now.LocalDateTime;
        Updated?.Invoke(this, new DataSourceUpdatedEventArgs<ClockSnapshot>(snapshot, LastUpdated));
    }

    /// <summary>
    /// 按当前配置格式化时间，供组件直接使用。
    /// </summary>
    public static string FormatTime(DateTimeOffset time, bool use24Hour, bool withSeconds)
    {
        var pattern = use24Hour
            ? (withSeconds ? "HH\\:mm\\:ss" : "HH\\:mm")
            : (withSeconds ? "h\\:mm\\:ss\\ tt" : "h\\:mm\\ tt");

        return time.ToString(pattern, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 按当前配置格式化日期。
    /// </summary>
    public static string FormatDate(DateTimeOffset time, bool showWeekday)
    {
        var pattern = showWeekday ? "yyyy/MM/dd ddd" : "yyyy/MM/dd";
        return time.ToString(pattern, CultureInfo.CurrentCulture);
    }
}