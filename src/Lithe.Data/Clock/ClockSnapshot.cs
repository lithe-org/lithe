namespace Lithe.Data.Clock;

/// <summary>
/// 时钟快照。
/// </summary>
/// <param name="Now">采集时刻，含时区偏移。</param>
public readonly record struct ClockSnapshot(DateTimeOffset Now)
{
    /// <summary>
    /// 本地日期。
    /// </summary>
    public DateOnly Date => DateOnly.FromDateTime(Now.LocalDateTime);

    /// <summary>
    /// 星期。
    /// </summary>
    public DayOfWeek Weekday => Now.DayOfWeek;

    /// <summary>
    /// 是否为周末。
    /// </summary>
    public bool IsWeekend => Now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}