using Lithe.Core.Data;

namespace Lithe.Data;

/// <summary>
/// 数据源注册表。所有 <see cref="IDataSource{T}"/> 必须注册到这里，由注册表统一管理启停。
/// </summary>
/// <remarks>
/// 禁止在 ViewModel 或模块中直接 <c>new</c> 数据源：同一类型的数据（如时间、天气）
/// 在整个应用内只允许存在一个实例，顶栏与桌面通过 <see cref="TryGet{T}"/> 订阅同一实例。
/// </remarks>
public sealed class DataSourceRegistry : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Registration> _registrations = [];

    /// <summary>
    /// 当前已注册的数据源 Id，按注册顺序排列。
    /// </summary>
    public IReadOnlyList<string> RegisteredIds
    {
        get
        {
            lock (_gate)
            {
                return [.. _registrations.Keys];
            }
        }
    }

    /// <summary>
    /// 注册数据源。Id 重复时抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <typeparam name="T">数据源提供的数据类型。</typeparam>
    /// <param name="source">数据源实例。</param>
    public void Register<T>(IDataSource<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.Id))
        {
            throw new ArgumentException("数据源 Id 不能为空。", nameof(source));
        }

        lock (_gate)
        {
            if (_registrations.ContainsKey(source.Id))
            {
                throw new InvalidOperationException(
                    $"数据源 Id \"{source.Id}\" 重复注册。同一类型的数据只允许存在一个实例。");
            }

            _registrations[source.Id] = new Registration(
                source.Id,
                source,
                source.StartAsync,
                source.StopAsync);
        }
    }

    /// <summary>
    /// 判断某个 Id 是否已注册。
    /// </summary>
    public bool IsRegistered(string id)
    {
        lock (_gate)
        {
            return _registrations.ContainsKey(id);
        }
    }

    /// <summary>
    /// 获取已注册的数据源实例。类型不匹配或未注册时返回 <see langword="false"/>。
    /// </summary>
    /// <typeparam name="T">数据源提供的数据类型。</typeparam>
    /// <param name="id">数据源 Id。</param>
    /// <param name="source">取出的数据源实例。</param>
    public bool TryGet<T>(string id, out IDataSource<T> source)
    {
        lock (_gate)
        {
            if (_registrations.TryGetValue(id, out var registration) && registration.Value is IDataSource<T> typed)
            {
                source = typed;
                return true;
            }
        }

        source = default!;
        return false;
    }

    /// <summary>
    /// 按 Id 取数据源实例，不校验数据类型。
    /// </summary>
    /// <remarks>
    /// 组件框架只认识数据源 Id，不关心具体类型，所以提供这个弱类型入口。
    /// </remarks>
    public bool TryGetSource(string id, out object? source)
    {
        lock (_gate)
        {
            if (_registrations.TryGetValue(id, out var registration))
            {
                source = registration.Value;
                return true;
            }
        }

        source = null;
        return false;
    }

    /// <summary>
    /// 判断数据源当前是否处于运行状态。
    /// </summary>
    public bool IsRunning(string id)
    {
        lock (_gate)
        {
            return _registrations.TryGetValue(id, out var registration) && registration.Running;
        }
    }

    /// <summary>
    /// 启动全部已注册的数据源。
    /// </summary>
    /// <returns>启动失败的数据源及其异常；全部成功时返回空列表。</returns>
    public async Task<IReadOnlyList<DataSourceFailure>> StartAllAsync()
    {
        return await StartStopAllAsync(StartOneAsync, running: true).ConfigureAwait(false);
    }

    /// <summary>
    /// 停止全部已注册的数据源。
    /// </summary>
    /// <returns>停止失败的数据源及其异常；全部成功时返回空列表。</returns>
    public async Task<IReadOnlyList<DataSourceFailure>> StopAllAsync()
    {
        return await StartStopAllAsync(StopOneAsync, running: false).ConfigureAwait(false);
    }

    private static Task StartOneAsync(Registration registration) => registration.StartAsync();

    private static Task StopOneAsync(Registration registration) => registration.StopAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAllAsync().ConfigureAwait(false);
        lock (_gate)
        {
            _registrations.Clear();
        }
    }

    private async Task<IReadOnlyList<DataSourceFailure>> StartStopAllAsync(Func<Registration, Task> operation, bool running)
    {
        Registration[] targets;
        lock (_gate)
        {
            targets = [.. _registrations.Values];
        }

        List<DataSourceFailure>? failures = null;
        foreach (var registration in targets)
        {
            try
            {
                await operation(registration).ConfigureAwait(false);
                lock (_gate)
                {
                    registration.Running = running;
                }
            }
            catch (Exception ex)
            {
                // 采集类数据源跑在后台线程，未捕获异常会直接终止主进程，这里兜底。
                (failures ??= []).Add(new DataSourceFailure(registration.Id, ex));
            }
        }

        return (IReadOnlyList<DataSourceFailure>?)failures ?? [];
    }

    private sealed class Registration
    {
        public Registration(string id, object value, Func<Task> start, Func<Task> stop)
        {
            Id = id;
            Value = value;
            StartAsync = start;
            StopAsync = stop;
        }

        public string Id { get; }

        public object Value { get; }

        public Func<Task> StartAsync { get; }

        public Func<Task> StopAsync { get; }

        public bool Running { get; set; }
    }
}

/// <summary>
/// 数据源启停失败记录。
/// </summary>
/// <param name="Id">数据源 Id。</param>
/// <param name="Error">失败原因。</param>
public readonly record struct DataSourceFailure(string Id, Exception Error);