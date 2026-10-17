namespace Lithe.Core.Events;

/// <summary>
/// 应用内事件总线。模块之间只通过事件通信，不直接相互引用。
/// </summary>
/// <remarks>
/// 线程模型：<see cref="Publish"/> 在发布者线程同步调用订阅者，不做线程切换。
/// 需要更新 UI 的订阅者自行通过 <c>DispatcherQueue</c> 切回 UI 线程。
/// </remarks>
public interface IEventBus
{
    /// <summary>
    /// 发布事件，同步调用所有该事件类型的订阅者。
    /// </summary>
    /// <typeparam name="TEvent">事件类型。</typeparam>
    /// <param name="evt">事件实例。</param>
    void Publish<TEvent>(TEvent evt);

    /// <summary>
    /// 订阅事件。
    /// </summary>
    /// <typeparam name="TEvent">事件类型。</typeparam>
    /// <param name="handler">处理委托。</param>
    /// <returns>订阅句柄，释放即取消订阅。</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
}