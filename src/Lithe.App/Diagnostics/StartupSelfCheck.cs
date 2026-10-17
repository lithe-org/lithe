using System.Diagnostics;
using Lithe.Core.Data;
using Lithe.Core.Events;
using Lithe.Data;
using Lithe.Data.Clock;
using Lithe.Data.Todo;

namespace Lithe_App.Diagnostics;

/// <summary>
/// 启动自检：把事件总线与数据源注册表的关键行为输出到调试输出，便于在没有 UI 的情况下验证地基。
/// 里程碑 1-2 的临时验证入口，接入真实界面后移除。
/// </summary>
internal static class StartupSelfCheck
{
    public static void Run()
    {
        EventBusSelfCheck();
        DataSourceRegistrySelfCheck();
        RealDataSourceSelfCheck();
    }

    private static void EventBusSelfCheck()
    {
        var bus = new EventBus();
        bus.SubscriberFaulted += (_, args) =>
            ShellDiagnostics.Write($"[自检] 订阅者异常已隔离: {args.EventType.Name} / {args.Exception.Message}");

        var probe = new Probe();
        using (bus.Subscribe<string>(probe.OnMessage))
        {
            bus.Publish("第一条消息");
            ShellDiagnostics.Write($"[自检] 事件订阅收到 {probe.Received.Count} 条消息: {string.Join(", ", probe.Received)}");
        }

        // 句柄释放后应立刻退订。
        bus.Publish("释放之后的消息");
        ShellDiagnostics.Write($"[自检] 退订后累计收到 {probe.Received.Count} 条消息");

        // 订阅者抛异常不影响发布者与其他订阅者。
        using (bus.Subscribe<string>(static _ => throw new InvalidOperationException("故意抛异常")))
        {
            bus.Publish("有订阅者会抛异常");
        }

        // 实例宿主被回收后弱引用订阅自动失效。
        CreateDisposableSubscriber(bus);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        bus.Publish("宿主回收之后的消息");
        ShellDiagnostics.Write($"[自检] 宿主回收后存活订阅数 {bus.GetSubscriberCount<string>()}（期望 0）");
    }

    private static void CreateDisposableSubscriber(EventBus bus)
    {
        var transient = new Probe();
        bus.Subscribe<string>(transient.OnMessage);
    }

    private static void DataSourceRegistrySelfCheck()
    {
        var registry = new DataSourceRegistry();
        registry.Register(new SelfCheckDataSource());

        var failures = registry.StartAllAsync().GetAwaiter().GetResult();
        ShellDiagnostics.Write($"[自检] 数据源启动完成，失败 {failures.Count} 个，运行中 {registry.IsRunning(SelfCheckDataSource.SourceId)}");
        ShellDiagnostics.Write($"[自检] 已注册数据源: {string.Join(", ", registry.RegisteredIds)}");

        if (registry.TryGet<int>(SelfCheckDataSource.SourceId, out var source))
        {
            ShellDiagnostics.Write($"[自检] 取到数据源 {source.Id}，当前值 {source.CurrentValue}，间隔 {source.RefreshInterval}");
        }
        else
        {
            ShellDiagnostics.Write("[自检] 取数据源失败");
        }

        Thread.Sleep(1200);
        ShellDiagnostics.Write($"[自检] 等待刷新后当前值 {registry.TryGet<int>(SelfCheckDataSource.SourceId, out var updated) && updated.CurrentValue > 0}（期望 True）");

        registry.StopAllAsync().GetAwaiter().GetResult();
        ShellDiagnostics.Write($"[自检] 数据源停止完成，运行中 {registry.IsRunning(SelfCheckDataSource.SourceId)}（期望 False）");
    }

    /// <summary>
    /// 真实数据源只做只读断言，避免启动自检往用户的待办文件里写测试数据。
    /// </summary>
    private static void RealDataSourceSelfCheck()
    {
        var registry = App.DataSources;
        if (registry is null)
        {
            return;
        }

        if (registry.TryGet<ClockSnapshot>(ClockDataSource.SourceId, out var clock))
        {
            var first = clock.CurrentValue;
            Thread.Sleep(1100);
            ShellDiagnostics.Write(
                $"[自检] 时钟数据源推送 {(clock.CurrentValue.Now - first.Now).TotalSeconds:F1}s，格式化 {ClockDataSource.FormatTime(clock.CurrentValue.Now, true, true)} / {ClockDataSource.FormatDate(first.Now, true)}");
        }
        else
        {
            ShellDiagnostics.Write("[自检] 取时钟数据源失败");
        }

        if (registry.TryGet<IReadOnlyList<TodoItem>>(TodoDataSource.SourceId, out var todos))
        {
            ShellDiagnostics.Write($"[自检] 待办数据源可用，当前 {todos.CurrentValue.Count} 条");
        }
        else
        {
            ShellDiagnostics.Write("[自检] 取待办数据源失败");
        }
    }

    private sealed class Probe
    {
        public List<string> Received { get; } = [];

        public void OnMessage(string message) => Received.Add(message);
    }

    private sealed class SelfCheckDataSource : IDataSource<int>
    {
        public const string SourceId = "self-check";

        private Timer? _timer;

        public string Id => SourceId;

        public int CurrentValue { get; private set; }

        public DateTime LastUpdated { get; private set; } = DateTime.MinValue;

        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(1);

        public event EventHandler<DataSourceUpdatedEventArgs<int>>? Updated;

        public Task StartAsync()
        {
            _timer = new Timer(_ => Tick(), null, RefreshInterval, RefreshInterval);
            Tick();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _timer?.Dispose();
            _timer = null;
            return Task.CompletedTask;
        }

        private void Tick()
        {
            CurrentValue++;
            LastUpdated = DateTime.Now;
            Updated?.Invoke(this, new DataSourceUpdatedEventArgs<int>(CurrentValue, LastUpdated));
        }
    }
}