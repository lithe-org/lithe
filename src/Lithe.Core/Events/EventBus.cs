using System.Reflection;

namespace Lithe.Core.Events;

/// <summary>
/// 轻量事件总线实现。
/// </summary>
/// <remarks>
/// <para>订阅者以弱引用持有：实例方法订阅的宿主对象被回收后，订阅自动失效并在下次发布时清理；
/// 静态委托（无宿主对象）则保持强引用，避免被误回收。</para>
/// <para>事件类型按精确类型匹配：<c>Subscribe&lt;Base&gt;</c> 不会收到 <c>Publish&lt;Derived&gt;</c>。</para>
/// </remarks>
public sealed class EventBus : IEventBus
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Type, List<HandlerBox>> _subscriptions = [];

    /// <summary>
    /// 订阅者抛出异常时触发。事件总线不会让单个订阅者的异常影响发布者和其他订阅者。
    /// </summary>
    public event EventHandler<SubscriberFaultedEventArgs>? SubscriberFaulted;

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var box = new HandlerBox<TEvent>(handler);
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var list))
            {
                list = [];
                _subscriptions[typeof(TEvent)] = list;
            }

            list.Add(box);
        }

        return new Subscription(this, typeof(TEvent), box);
    }

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent evt)
    {
        HandlerBox[] snapshot;
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
            {
                return;
            }

            snapshot = [.. list];
        }

        List<HandlerBox>? dead = null;
        foreach (var box in snapshot)
        {
            if (!box.IsAlive)
            {
                (dead ??= []).Add(box);
                continue;
            }

            try
            {
                box.Invoke(evt!);
            }
            catch (Exception ex)
            {
                OnSubscriberFaulted(typeof(TEvent), ex);
            }
        }

        if (dead is not null)
        {
            lock (_gate)
            {
                foreach (var box in dead)
                {
                    Remove(typeof(TEvent), box);
                }
            }
        }
    }

    /// <summary>
    /// 当前某个事件类型的有效订阅数，仅用于诊断。
    /// </summary>
    public int GetSubscriberCount<TEvent>()
    {
        lock (_gate)
        {
            return _subscriptions.TryGetValue(typeof(TEvent), out var list)
                ? list.Count(static box => box.IsAlive)
                : 0;
        }
    }

    private void OnSubscriberFaulted(Type eventType, Exception exception)
    {
        var handler = SubscriberFaulted;
        if (handler is null)
        {
            return;
        }

        try
        {
            handler(this, new SubscriberFaultedEventArgs(eventType, exception));
        }
        catch
        {
            // 故障通知本身再抛异常就只能丢弃，否则会陷入递归。
        }
    }

    private void Remove(Type eventType, HandlerBox box)
    {
        if (!_subscriptions.TryGetValue(eventType, out var list))
        {
            return;
        }

        list.Remove(box);
        if (list.Count == 0)
        {
            _subscriptions.Remove(eventType);
        }
    }

    /// <summary>
    /// 订阅句柄，释放即取消订阅。
    /// </summary>
    private sealed class Subscription(EventBus owner, Type eventType, HandlerBox box) : IDisposable
    {
        private EventBus? _owner = owner;

        public void Dispose()
        {
            var bus = Interlocked.Exchange(ref _owner, null);
            if (bus is null)
            {
                return;
            }

            lock (bus._gate)
            {
                bus.Remove(eventType, box);
            }
        }
    }

    /// <summary>
    /// 非泛型订阅容器，屏蔽泛型类型，供字典存储使用。
    /// </summary>
    private abstract class HandlerBox
    {
        public abstract bool IsAlive { get; }

        public abstract void Invoke(object payload);
    }

    private sealed class HandlerBox<TEvent> : HandlerBox
    {
        private readonly Action<TEvent>? _strongHandler;
        private readonly WeakReference<object>? _targetReference;
        private readonly MethodInfo? _method;

        public HandlerBox(Action<TEvent> handler)
        {
            if (handler.Target is null)
            {
                // 静态委托没有宿主对象，只能强引用持有。
                _strongHandler = handler;
                return;
            }

            _targetReference = new WeakReference<object>(handler.Target);
            _method = handler.Method;
        }

        public override bool IsAlive =>
            _strongHandler is not null || _targetReference!.TryGetTarget(out _);

        public override void Invoke(object payload)
        {
            if (_strongHandler is not null)
            {
                _strongHandler((TEvent)payload);
                return;
            }

            if (_targetReference!.TryGetTarget(out var target))
            {
                var handler = (Action<TEvent>)_method!.CreateDelegate(typeof(Action<TEvent>), target);
                handler((TEvent)payload);
            }
        }
    }
}

/// <summary>
/// 订阅者抛出异常时携带的信息。
/// </summary>
/// <param name="eventType">发布时的事件类型。</param>
/// <param name="exception">订阅者抛出的异常。</param>
public sealed class SubscriberFaultedEventArgs(Type eventType, Exception exception) : EventArgs
{
    /// <summary>
    /// 发布时的事件类型。
    /// </summary>
    public Type EventType { get; } = eventType;

    /// <summary>
    /// 订阅者抛出的异常。
    /// </summary>
    public Exception Exception { get; } = exception;
}