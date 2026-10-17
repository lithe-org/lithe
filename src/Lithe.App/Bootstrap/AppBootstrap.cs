using Lithe.Config;
using Lithe.Core.Diagnostics;
using Lithe.Data;
using Lithe.Data.Clock;
using Lithe.Data.Todo;

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

    /// <summary>
    /// 注册并启动全部数据源。
    /// </summary>
    public static DataSourceRegistry InitializeDataSources()
    {
        var registry = new DataSourceRegistry();
        registry.Register(new ClockDataSource());
        registry.Register(new TodoDataSource(ConfigPaths.TodosFile));

        // 启动阶段无 UI 依赖，同步等待即可；单个数据源失败由注册表兜底。
        var failures = registry.StartAllAsync().GetAwaiter().GetResult();
        foreach (var failure in failures)
        {
            AppLog.Warn($"数据源 {failure.Id} 启动失败", failure.Error);
        }

        AppLog.Info($"已启动数据源：{string.Join(", ", registry.RegisteredIds)}");

        return registry;
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