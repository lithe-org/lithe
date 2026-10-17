namespace Lithe.Core.Diagnostics;

/// <summary>
/// 应用日志出口。各模块只依赖这个接口，正式日志系统在里程碑 6 接入。
/// </summary>
public interface IAppLogger
{
    /// <summary>
    /// 常规信息。
    /// </summary>
    void Info(string message);

    /// <summary>
    /// 可恢复的异常情况。
    /// </summary>
    void Warn(string message, Exception? exception = null);

    /// <summary>
    /// 不可恢复的错误。
    /// </summary>
    void Error(string message, Exception? exception = null);
}

/// <summary>
/// 全局日志入口。未注册实现时静默丢弃，避免日志本身成为崩溃源。
/// </summary>
public static class AppLog
{
    private static IAppLogger? s_logger;

    /// <summary>
    /// 注册日志实现，通常在应用启动时调用一次。
    /// </summary>
    public static void SetLogger(IAppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        s_logger = logger;
    }

    /// <summary>
    /// 写常规信息。
    /// </summary>
    public static void Info(string message) => s_logger?.Info(message);

    /// <summary>
    /// 写可恢复的异常情况。
    /// </summary>
    public static void Warn(string message, Exception? exception = null) => s_logger?.Warn(message, exception);

    /// <summary>
    /// 写不可恢复的错误。
    /// </summary>
    public static void Error(string message, Exception? exception = null) => s_logger?.Error(message, exception);
}