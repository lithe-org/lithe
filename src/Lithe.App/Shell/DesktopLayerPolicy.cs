using Lithe.Core.Config;

namespace Lithe_App.Shell;

/// <summary>
/// 桌面层启停与挂载决策。纯静态函数：只依赖配置与文件系统事实，
/// 不碰窗口、线程与 IO 之外的副作用，便于单测与 AOT。
/// </summary>
public static class DesktopLayerPolicy
{
    /// <summary>
    /// 桌面层是否启用：配置项关闭或存在自救标记文件时禁用。
    /// </summary>
    /// <param name="config">应用级配置服务。</param>
    /// <param name="rootDirectory">配置根目录（<c>.lithe</c>）。</param>
    public static bool IsEnabled(IConfigService config, string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        return config.GetOrDefault("desktop_layer.enabled", true)
            && !File.Exists(Path.Combine(rootDirectory, "disable-desktop-layer.flag"));
    }

    /// <summary>
    /// 是否挂载到桌面宿主（Progman）。默认挂载；配置关掉后，
    /// <c>attach-desktop-layer.flag</c> 仍可强制挂载，供出问题时快速验证。
    /// </summary>
    /// <param name="config">应用级配置服务。</param>
    /// <param name="rootDirectory">配置根目录（<c>.lithe</c>）。</param>
    public static bool ShouldAttach(IConfigService config, string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        return config.GetOrDefault("desktop_layer.attach_to_desktop_host", true)
            || File.Exists(Path.Combine(rootDirectory, "attach-desktop-layer.flag"));
    }
}
