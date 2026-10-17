namespace Lithe.Config;

/// <summary>
/// 配置目录结构，根目录固定为 <c>%USERPROFILE%\.lithe\</c>。
/// </summary>
public static class ConfigPaths
{
    private static readonly Lazy<string> s_root = new(static () => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".lithe"));

    /// <summary>
    /// 配置根目录。
    /// </summary>
    public static string RootDirectory => s_root.Value;

    /// <summary>
    /// 主配置。
    /// </summary>
    public static string ConfigFile => Path.Combine(RootDirectory, "config.toml");

    /// <summary>
    /// 布局配置（网格坐标、组件尺寸）。
    /// </summary>
    public static string LayoutFile => Path.Combine(RootDirectory, "layout.toml");

    /// <summary>
    /// 待办数据。
    /// </summary>
    public static string TodosFile => Path.Combine(RootDirectory, "todos.toml");

    /// <summary>
    /// 通知持久化，默认不生成。
    /// </summary>
    public static string NotificationsFile => Path.Combine(RootDirectory, "notifications.toml");

    /// <summary>
    /// 配色方案目录。
    /// </summary>
    public static string ThemesDirectory => Path.Combine(RootDirectory, "themes");

    /// <summary>
    /// 组件模板目录。
    /// </summary>
    public static string TemplatesDirectory => Path.Combine(RootDirectory, "templates");

    /// <summary>
    /// 日志目录。
    /// </summary>
    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    /// <summary>
    /// 创建配置目录结构。已存在时不做处理。
    /// </summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ThemesDirectory);
        Directory.CreateDirectory(TemplatesDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}