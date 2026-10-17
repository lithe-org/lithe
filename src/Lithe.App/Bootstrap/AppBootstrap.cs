using Lithe.Config;
using Lithe.Config.Toml;
using Lithe.Core.Diagnostics;
using Lithe.Core.Layout;
using Lithe.Data;
using Lithe.Data.Clock;
using Lithe.Data.Todo;
using Lithe.Layout;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;

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

    /// <summary>
    /// 初始化布局引擎与编辑模式，并把布局变更实时写回 <c>layout.toml</c>。
    /// </summary>
    public static (LayoutEngine Layout, EditModeService EditMode) InitializeLayout()
    {
        var spacing = App.Configuration.GetOrDefault("layout.grid_spacing", 8);
        var calculator = new GridCalculator(spacing);
        var metrics = calculator.Calculate(GetPrimaryDisplay());

        var layout = new LayoutEngine(metrics);
        var editMode = new EditModeService();

        var restored = LoadLayout(layout);
        AppLog.Info($"布局网格 {metrics.Columns}x{metrics.Rows}，单元 {metrics.CellWidth:F0}x{metrics.CellHeight:F0}，已恢复组件 {restored} 个");

        layout.LayoutChanged += (_, _) => SaveLayout(layout);
        editMode.EditModeChanged += (_, _) =>
            AppLog.Info(editMode.IsEditing ? "进入编辑模式" : "退出编辑模式");

        return (layout, editMode);
    }

    /// <summary>
    /// 读取主显示器的工作区尺寸。
    /// </summary>
    public static DisplayMetrics GetPrimaryDisplay()
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(App.Window));
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = area.WorkArea;

        return new DisplayMetrics(workArea.Width, workArea.Height, workArea.Width / Math.Max(1.0, area.WorkArea.Width));
    }

    private static void EnsureLayoutFile()
    {
        if (!File.Exists(ConfigPaths.LayoutFile))
        {
            File.WriteAllText(ConfigPaths.LayoutFile, TomlSerializer.Serialize(DefaultConfig.CreateLayout()));
        }
    }

    private static int LoadLayout(LayoutEngine layout)
    {
        try
        {
            var table = TomlSerializer.Parse(File.ReadAllText(ConfigPaths.LayoutFile));
            if (table.Find("widgets") is not TomlArray { Items.Count: > 0 } widgets)
            {
                return 0;
            }

            var placements = new List<WidgetPlacement>();
            foreach (var node in widgets.Items.OfType<TomlTable>())
            {
                if (ReadPlacement(node) is { } placement)
                {
                    placements.Add(placement);
                }
            }

            return layout.Load(placements);
        }
        catch (Exception ex)
        {
            AppLog.Warn("布局文件读取失败，已从空布局启动", ex);
            return 0;
        }
    }

    private static WidgetPlacement? ReadPlacement(TomlTable node)
    {
        if (node["id"] is not TomlScalar { Value: string id } || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var column = ReadInt(node, "col");
        var row = ReadInt(node, "row");
        var colSpan = Math.Max(1, ReadInt(node, "col_span", 1));
        var rowSpan = Math.Max(1, ReadInt(node, "row_span", 1));

        return new WidgetPlacement(id, new GridCell(column, row, colSpan, rowSpan));
    }

    private static int ReadInt(TomlTable node, string key, int fallback = 0)
    {
        return node[key] is TomlScalar { Value: long value } ? (int)value : fallback;
    }

    private static void SaveLayout(LayoutEngine layout)
    {
        try
        {
            var table = new TomlTable();
            table.SetPath("meta.version", TomlNode.Scalar(1));
            table.SetPath("widgets", new TomlArray([.. layout.GetAllPlacements().Select(WritePlacement)]));

            var temp = ConfigPaths.LayoutFile + ".tmp";
            File.WriteAllText(temp, TomlSerializer.Serialize(table));
            File.Move(temp, ConfigPaths.LayoutFile, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Warn("布局保存失败", ex);
        }
    }

    private static TomlTable WritePlacement(WidgetPlacement placement)
    {
        var node = new TomlTable();
        node.SetPath("id", TomlNode.Scalar(placement.WidgetId));
        node.SetPath("col", TomlNode.Scalar(placement.Cell.Column));
        node.SetPath("row", TomlNode.Scalar(placement.Cell.Row));
        node.SetPath("col_span", TomlNode.Scalar(placement.Cell.ColSpan));
        node.SetPath("row_span", TomlNode.Scalar(placement.Cell.RowSpan));
        return node;
    }
}