namespace Lithe.Core.Data;

/// <summary>
/// 数据源。同一类型的数据在应用内只允许存在一个实例，统一注册到数据源注册表。
/// </summary>
/// <typeparam name="T">该数据源提供的数据类型。</typeparam>
public interface IDataSource<T>
{
    /// <summary>
    /// 数据源唯一标识，顶栏与桌面通过它订阅同一实例。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 最新一次采集到的数据。
    /// </summary>
    T CurrentValue { get; }

    /// <summary>
    /// 最近一次更新时间。
    /// </summary>
    DateTime LastUpdated { get; }

    /// <summary>
    /// 刷新间隔。刷新率按数据源配置，而非按组件实例。
    /// </summary>
    TimeSpan RefreshInterval { get; set; }

    /// <summary>
    /// 数据更新事件。
    /// </summary>
    event EventHandler<DataSourceUpdatedEventArgs<T>>? Updated;

    /// <summary>
    /// 启动采集。由注册表统一调用，模块不直接启停数据源。
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 停止采集。由注册表统一调用。
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// 数据源更新事件参数。
/// </summary>
/// <typeparam name="T">数据类型。</typeparam>
/// <param name="value">新值。</param>
/// <param name="timestamp">更新时间。</param>
public sealed class DataSourceUpdatedEventArgs<T>(T value, DateTime timestamp) : EventArgs
{
    /// <summary>
    /// 新值。
    /// </summary>
    public T Value { get; } = value;

    /// <summary>
    /// 更新时间。
    /// </summary>
    public DateTime Timestamp { get; } = timestamp;
}