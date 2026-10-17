using Lithe.Config.Toml;

namespace Lithe.Config;

/// <summary>
/// 默认配置内容。首次启动生成 <c>config.toml</c> 时作为基线。
/// </summary>
public static class DefaultConfig
{
    /// <summary>
    /// 生成默认配置表。
    /// </summary>
    public static TomlTable Create()
    {
        var table = new TomlTable();

        table.SetPath("general.language", TomlNode.Scalar("system"));
        table.SetPath("general.theme", TomlNode.Scalar("system"));
        table.SetPath("general.accent", TomlNode.Scalar("#0078D4"));

        table.SetPath("dock.enabled", TomlNode.Scalar(true));
        table.SetPath("dock.icon_size", TomlNode.Scalar(48));
        table.SetPath("dock.height", TomlNode.Scalar(64));
        table.SetPath("dock.auto_hide", TomlNode.Scalar(false));
        table.SetPath("dock.spacing", TomlNode.Scalar(8));

        table.SetPath("topbar.enabled", TomlNode.Scalar(true));
        table.SetPath("topbar.height", TomlNode.Scalar(32));
        table.SetPath("topbar.background", TomlNode.Scalar("acrylic"));
        table.SetPath("topbar.show_seconds", TomlNode.Scalar(false));

        table.SetPath("notifications.persist", TomlNode.Scalar(false));
        table.SetPath("notifications.memory_limit", TomlNode.Scalar(50));
        table.SetPath("notifications.disk_limit", TomlNode.Scalar(100));

        table.SetPath("layout.grid_spacing", TomlNode.Scalar(8));

        // 桌面层可能出现遮挡或交互异常，出问题时可先在这里关掉。
        table.SetPath("desktop_layer.enabled", TomlNode.Scalar(true));

        // 默认挂到 Progman：组件位于所有窗口下方、壁纸上方，不抢占置顶。
        // 挂载失败（找不到 Progman 等）自动降级为顶层置顶窗口，日志见 ShellDiagnostics。
        table.SetPath("desktop_layer.attach_to_desktop_host", TomlNode.Scalar(true));

        return table;
    }

    /// <summary>
    /// 生成默认布局表。组件清单在布局引擎接入前为空。
    /// </summary>
    public static TomlTable CreateLayout()
    {
        var layout = new TomlTable();
        layout.SetPath("meta.version", TomlNode.Scalar(1));
        layout.SetPath("widgets", new TomlArray());
        return layout;
    }
}