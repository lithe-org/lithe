using Tomlyn;
using Tomlyn.Serialization;

namespace Lithe.Config.Toml;

/// <summary>
/// TOML 文本与 <see cref="TomlTable"/> 的互转。
/// </summary>
/// <remarks>
/// 直接使用 Tomlyn 的读取器与写入器，不做反射映射，NativeAOT 下行为一致。
/// </remarks>
public static class TomlSerializer
{
    private static readonly TomlSerializerOptions s_options = new() { WriteIndented = true };

    /// <summary>
    /// 解析 TOML 文本。空文本视为空表。
    /// </summary>
    public static TomlTable Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TomlTable();
        }

        try
        {
            return ParseCore(text);
        }
        catch (TomlException ex)
        {
            // 不让第三方异常类型泄漏到上层。
            throw new FormatException($"TOML 解析失败：{ex.Message}", ex);
        }
    }

    private static TomlTable ParseCore(string text)
    {
        var reader = TomlReader.Create(text, s_options);

        if (!reader.Read() || reader.TokenType != TomlTokenType.StartDocument)
        {
            throw new FormatException("TOML 文档结构无效。");
        }

        // 根节点是一个匿名表。
        if (!reader.Read())
        {
            return new TomlTable();
        }

        return reader.TokenType switch
        {
            TomlTokenType.StartTable => ReadTable(reader),
            TomlTokenType.EndDocument => new TomlTable(),
            _ => throw new FormatException($"TOML 文档根节点无效：{reader.TokenType}。"),
        };
    }

    /// <summary>
    /// 序列化为 TOML 文本。
    /// </summary>
    public static string Serialize(TomlTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        using var buffer = new StringWriter();
        var writer = new TomlWriter(buffer, s_options);
        writer.WriteStartDocument();
        WriteTableBody(writer, table);
        writer.WriteEndDocument();
        return buffer.ToString();
    }

    private static TomlTable ReadTable(TomlReader reader)
    {
        var table = new TomlTable();

        while (reader.Read())
        {
            if (reader.TokenType == TomlTokenType.EndTable)
            {
                return table;
            }

            if (reader.TokenType != TomlTokenType.PropertyName)
            {
                throw new FormatException($"第 {reader.Line} 行出现意外的 {reader.TokenType}。");
            }

            var key = reader.PropertyName
                ?? throw new FormatException($"第 {reader.Line} 行出现了没有名字的键。");
            if (!reader.Read())
            {
                throw new FormatException($"键 \"{key}\" 缺少值。");
            }

            table[key] = ReadNode(reader);
        }

        throw new FormatException("表未正常结束。");
    }

    private static TomlArray ReadArray(TomlReader reader)
    {
        var array = new TomlArray();

        while (reader.Read())
        {
            if (reader.TokenType == TomlTokenType.EndArray)
            {
                return array;
            }

            array.Items.Add(ReadNode(reader));
        }

        throw new FormatException("数组未正常结束。");
    }

    private static TomlNode ReadNode(TomlReader reader)
    {
        return reader.TokenType switch
        {
            TomlTokenType.StartTable => ReadTable(reader),
            TomlTokenType.StartArray => ReadArray(reader),
            TomlTokenType.String => TomlNode.Scalar(reader.GetString()),
            TomlTokenType.Integer => TomlNode.Scalar(reader.GetInt64()),
            TomlTokenType.Float => TomlNode.Scalar(reader.GetDouble()),
            TomlTokenType.Boolean => TomlNode.Scalar(reader.GetBoolean()),
            TomlTokenType.DateTime => TomlNode.Scalar(reader.GetTomlDateTime().DateTime),
            _ => throw new FormatException($"第 {reader.Line} 行出现不支持的值类型 {reader.TokenType}。"),
        };
    }

    private static void WriteTableBody(TomlWriter writer, TomlTable table)
    {
        writer.WriteStartTable();
        foreach (var (key, node) in table.Entries())
        {
            writer.WritePropertyName(key);
            WriteNode(writer, node);
        }

        writer.WriteEndTable();
    }

    private static void WriteNode(TomlWriter writer, TomlNode node)
    {
        switch (node)
        {
            case TomlTable table:
                WriteTableBody(writer, table);
                break;

            case TomlArray array:
                writer.WriteStartArray();
                foreach (var item in array.Items)
                {
                    WriteArrayItem(writer, item);
                }

                writer.WriteEndArray();
                break;

            case TomlScalar scalar:
                WriteScalar(writer, scalar);
                break;

            default:
                throw new FormatException($"不支持的节点类型：{node.GetType().Name}");
        }
    }

    /// <summary>
    /// 数组内的表使用内联表写出，读回来的结构与表数组一致。
    /// </summary>
    private static void WriteArrayItem(TomlWriter writer, TomlNode node)
    {
        if (node is TomlTable table)
        {
            writer.WriteStartInlineTable();
            foreach (var (key, child) in table.Entries())
            {
                writer.WritePropertyName(key);
                WriteNode(writer, child);
            }

            writer.WriteEndInlineTable();
            return;
        }

        WriteNode(writer, node);
    }

    private static void WriteScalar(TomlWriter writer, TomlScalar scalar)
    {
        switch (scalar.Kind)
        {
            case TomlValueKind.String:
                writer.WriteStringValue((string)scalar.Value);
                break;
            case TomlValueKind.Integer:
                writer.WriteIntegerValue((long)scalar.Value);
                break;
            case TomlValueKind.Float:
                writer.WriteFloatValue((double)scalar.Value);
                break;
            case TomlValueKind.Boolean:
                writer.WriteBooleanValue((bool)scalar.Value);
                break;
            case TomlValueKind.DateTime:
                writer.WriteDateTimeValue(new TomlDateTime(ToOffset(scalar.Value), 0, TomlDateTimeKind.OffsetDateTimeByZ));
                break;
            default:
                throw new FormatException($"不支持的标量类型：{scalar.Kind}");
        }
    }

    private static DateTimeOffset ToOffset(object value)
    {
        return value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(dateTime, TimeSpan.Zero),
            _ => throw new FormatException($"不是合法的时间值：{value}"),
        };
    }
}