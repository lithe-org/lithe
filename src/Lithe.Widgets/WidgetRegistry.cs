namespace Lithe.Widgets;

/// <summary>
/// 组件注册表。桌面与顶栏都从这里按 Id 找组件工厂。
/// </summary>
public sealed class WidgetRegistry
{
    private readonly Dictionary<string, IWidgetFactory> _factories = [];

    /// <summary>
    /// 注册组件。
    /// </summary>
    public void Register(IWidgetFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (_factories.ContainsKey(factory.Definition.Id))
        {
            throw new InvalidOperationException($"组件 \"{factory.Definition.Id}\" 重复注册。");
        }

        _factories[factory.Definition.Id] = factory;
    }

    /// <summary>
    /// 取组件工厂。
    /// </summary>
    public bool TryGet(string widgetId, out IWidgetFactory factory) => _factories.TryGetValue(widgetId, out factory!);

    /// <summary>
    /// 全部已注册组件。
    /// </summary>
    public IReadOnlyList<IWidgetFactory> All => [.. _factories.Values];
}