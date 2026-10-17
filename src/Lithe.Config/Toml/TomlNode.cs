namespace Lithe.Config.Toml;

/// <summary>
/// TOML 值类型。
/// </summary>
public enum TomlValueKind
{
    /// <summary>
    /// 字符串。
    /// </summary>
    String,

    /// <summary>
    /// 整数。
    /// </summary>
    Integer,

    /// <summary>
    /// 浮点数。
    /// </summary>
    Float,

    /// <summary>
    /// 布尔值。
    /// </summary>
    Boolean,

    /// <summary>
    /// 日期时间。
    /// </summary>
    DateTime,
}

/// <summary>
/// TOML 文档节点基类。
/// </summary>
public abstract class TomlNode
{
    /// <summary>
    /// 标量节点。
    /// </summary>
    /// <param name="value">标量值，只接受字符串、整数、浮点、布尔、日期时间。</param>
    /// <exception cref="ArgumentException">值类型不受支持时抛出。</exception>
    public static TomlNode Scalar(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // 整数统一存为 long，浮点统一存为 double，避免同一数值出现多种表示。
        return value switch
        {
            string s => new TomlScalar(s),
            long l => new TomlScalar(l),
            int i => new TomlScalar((long)i),
            short s => new TomlScalar((long)s),
            byte b => new TomlScalar((long)b),
            bool boolean => new TomlScalar(boolean),
            double d => new TomlScalar(d),
            float f => new TomlScalar((double)f),
            DateTimeOffset offset => new TomlScalar(offset),
            DateTime dateTime => new TomlScalar(new DateTimeOffset(dateTime, TimeSpan.Zero)),
            _ => throw new ArgumentException($"不支持的 TOML 标量类型：{value.GetType().Name}", nameof(value)),
        };
    }

    /// <summary>
    /// 由 .NET 值构造节点，整数类型统一存为 <see cref="long"/>。
    /// </summary>
    public static TomlNode FromValue(object? value)
    {
        return value switch
        {
            null => new TomlScalar(string.Empty),
            string or long or bool or double or DateTimeOffset or DateTime => new TomlScalar(value),
            int i => new TomlScalar((long)i),
            short s => new TomlScalar((long)s),
            byte b => new TomlScalar((long)b),
            float f => new TomlScalar((double)f),
            decimal d => new TomlScalar((double)d),
            Enum e => new TomlScalar(e.ToString()),
            _ => new TomlScalar(value.ToString() ?? string.Empty),
        };
    }
}

/// <summary>
/// 标量节点。
/// </summary>
/// <param name="value">标量值。</param>
public sealed class TomlScalar(object value) : TomlNode
{
    /// <summary>
    /// 标量值，类型为字符串、<see cref="long"/>、<see cref="double"/>、<see cref="bool"/> 或 <see cref="DateTimeOffset"/>。
    /// </summary>
    public object Value { get; } = value;

    /// <summary>
    /// 标量类型。
    /// </summary>
    public TomlValueKind Kind => Value switch
    {
        string => TomlValueKind.String,
        long => TomlValueKind.Integer,
        double => TomlValueKind.Float,
        bool => TomlValueKind.Boolean,
        _ => TomlValueKind.DateTime,
    };

    /// <inheritdoc />
    public override string ToString() => Value.ToString() ?? string.Empty;
}

/// <summary>
/// 表节点，键值对保持写入顺序。
/// </summary>
public sealed class TomlTable : TomlNode
{
    private readonly Dictionary<string, TomlNode> _entries = [];
    private readonly List<string> _order = [];

    /// <summary>
    /// 创建空表。
    /// </summary>
    public TomlTable()
    {
    }

    /// <summary>
    /// 键的写入顺序。
    /// </summary>
    public IReadOnlyList<string> Keys => _order;

    /// <summary>
    /// 子节点数量。
    /// </summary>
    public int Count => _order.Count;

    /// <summary>
    /// 取或设子节点。赋值为 <see langword="null"/> 时删除该键。
    /// </summary>
    public TomlNode? this[string key]
    {
        get => _entries.GetValueOrDefault(key);
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            if (value is null)
            {
                Remove(key);
                return;
            }

            if (!_entries.ContainsKey(key))
            {
                _order.Add(key);
            }

            _entries[key] = value;
        }
    }

    /// <summary>
    /// 取子节点。
    /// </summary>
    public bool TryGetValue(string key, out TomlNode? node) => _entries.TryGetValue(key, out node);

    /// <summary>
    /// 是否包含某个键。
    /// </summary>
    public bool ContainsKey(string key) => _entries.ContainsKey(key);

    /// <summary>
    /// 删除子节点。
    /// </summary>
    public bool Remove(string key)
    {
        if (!_entries.Remove(key))
        {
            return false;
        }

        _order.Remove(key);
        return true;
    }

    /// <summary>
    /// 按写入顺序遍历子节点。
    /// </summary>
    public IEnumerable<KeyValuePair<string, TomlNode>> Entries()
    {
        foreach (var key in _order)
        {
            yield return new KeyValuePair<string, TomlNode>(key, _entries[key]);
        }
    }

    /// <summary>
    /// 按点分路径取子节点，中间层必须是表。
    /// </summary>
    public TomlNode? Find(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        TomlNode? current = this;
        foreach (var segment in path.Split('.'))
        {
            if (current is not TomlTable table || !table.TryGetValue(segment, out var child))
            {
                return null;
            }

            current = child;
        }

        return current;
    }

    /// <summary>
    /// 按点分路径设置子节点，自动创建中间的表。
    /// </summary>
    public void SetPath(string path, TomlNode value)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(value);

        var segments = path.Split('.');
        var current = this;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!current.TryGetValue(segments[i], out var child) || child is not TomlTable childTable)
            {
                childTable = new TomlTable();
                current[segments[i]] = childTable;
            }

            current = childTable;
        }

        current[segments[^1]] = value;
    }

    /// <summary>
    /// 按点分路径删除子节点。
    /// </summary>
    public bool RemovePath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var segments = path.Split('.');
        var current = this;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!current.TryGetValue(segments[i], out var child) || child is not TomlTable childTable)
            {
                return false;
            }

            current = childTable;
        }

        return current.Remove(segments[^1]);
    }
}

/// <summary>
/// 数组节点。
/// </summary>
/// <param name="items">数组元素。</param>
public sealed class TomlArray(List<TomlNode> items) : TomlNode
{
    /// <summary>
    /// 创建空数组。
    /// </summary>
    public TomlArray()
        : this([])
    {
    }

    /// <summary>
    /// 数组元素。
    /// </summary>
    public List<TomlNode> Items { get; } = items;
}