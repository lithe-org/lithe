using System.Diagnostics;
using Lithe.Config;
using Lithe.Core.Diagnostics;

namespace Lithe_App.Diagnostics;

/// <summary>
/// 桌面层与窗口相关的运行期诊断输出。
/// </summary>
/// <remarks>
/// 里程碑 1-2 期间先用最简实现把关键状态落到 <c>.lithe\logs\shell.log</c>，
/// 正式日志系统接入后由其接管。
/// </remarks>
internal sealed class ShellDiagnostics : IAppLogger
{
    /// <summary>
    /// 注册为全局日志实现。
    /// </summary>
    public static void Install()
    {
        AppLog.SetLogger(new ShellDiagnostics());
    }

    /// <inheritdoc />
    public void Info(string message) => Write(message);

    /// <inheritdoc />
    public void Warn(string message, Exception? exception = null) =>
        Write(exception is null ? $"警告：{message}" : $"警告：{message} / {exception.Message}");

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null) =>
        Write(exception is null ? $"错误：{message}" : $"错误：{message} / {exception}");

    /// <summary>
    /// 写一行诊断。
    /// </summary>
    public static void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        Debug.WriteLine(line);

        try
        {
            var path = Path.Combine(ConfigPaths.LogsDirectory, "shell.log");
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[诊断] 写日志失败：{ex.Message}");
        }
    }
}