using Lithe.Core.Config;
using Lithe.Core.Data;
using Lithe.Core.Events;
using Microsoft.UI.Dispatching;

namespace Lithe.Widgets;

/// <summary>
/// 组件运行上下文。组件只通过它拿数据源、配置与事件总线，不直接依赖具体实现。
/// </summary>
public sealed class WidgetContext
{
    /// <summary>
    /// 创建组件上下文。
    /// </summary>
    public WidgetContext(
        IEventBus eventBus,
        Func<string, object?> resolveDataSource,
        IConfigService? config = null,
        DispatcherQueue? dispatcher = null)
    {
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        ResolveDataSource = resolveDataSource ?? throw new ArgumentNullException(nameof(resolveDataSource));
        Config = config;
        DispatcherQueue = dispatcher;
    }

    /// <summary>
    /// 按 Id 解析数据源实例的委托，由宿主注入。
    /// </summary>
    public Func<string, object?> ResolveDataSource { get; }

    /// <summary>
    /// 事件总线。
    /// </summary>
    public IEventBus EventBus { get; }

    /// <summary>
    /// 配置服务，可能为空（组件要能容忍没有配置的情况）。
    /// </summary>
    public IConfigService? Config { get; }

    /// <summary>
    /// UI 线程调度器。
    /// </summary>
    public DispatcherQueue? DispatcherQueue { get; }

    /// <summary>
    /// 按 Id 取数据源实例。
    /// </summary>
    /// <param name="dataSourceId">数据源 Id。</param>
    public T? GetDataSource<T>(string dataSourceId)
        where T : class
    {
        return ResolveDataSource(dataSourceId) as T;
    }

    /// <summary>
    /// 在 UI 线程执行操作；拿不到调度器时直接执行。
    /// </summary>
    public void RunOnUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (DispatcherQueue is null)
        {
            action();
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
    }

    /// <summary>
    /// 读取组件配置项，缺失时返回默认值。
    /// </summary>
    public T GetSetting<T>(string widgetId, string key, T defaultValue)
    {
        if (Config is null)
        {
            return defaultValue;
        }

        return Config.GetOrDefault($"widgets.{widgetId}.{key}", defaultValue);
    }

    /// <summary>
    /// 写入组件配置项。
    /// </summary>
    public void SetSetting<T>(string widgetId, string key, T value)
    {
        Config?.Set($"widgets.{widgetId}.{key}", value);
    }
}