using Lithe.Config;

namespace Lithe_App.Bootstrap;

/// <summary>
/// 启动装配。后续模块的注册入口都挂在这里，App 只负责调用。
/// </summary>
internal static class AppBootstrap
{
    /// <summary>
    /// 准备配置目录与配置文件，并保证 <c>config.toml</c>、<c>layout.toml</c> 存在。
    /// </summary>
    public static TomlConfigService InitializeConfig()
    {
        ConfigPaths.EnsureDirectories();

        var config = new TomlConfigService(ConfigPaths.ConfigFile, DefaultConfig.Create());

        // 启动阶段无 UI 依赖，同步等待即可。
        config.LoadAsync().GetAwaiter().GetResult();
        config.SaveAsync().GetAwaiter().GetResult();

        EnsureLayoutFile();

        return config;
    }

    private static void EnsureLayoutFile()
    {
        if (File.Exists(ConfigPaths.LayoutFile))
        {
            return;
        }

        var layout = Lithe.Config.Toml.TomlSerializer.Serialize(DefaultConfig.CreateLayout());
        File.WriteAllText(ConfigPaths.LayoutFile, layout);
    }
}