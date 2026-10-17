using Lithe.Config;
using Lithe.Config.Toml;
using Lithe.Core.Data;
using Lithe.Core.Diagnostics;

namespace Lithe.Data.Todo;

/// <summary>
/// 待办数据源。事件驱动：只有增删改才会推送更新，并防抖写入 <c>todos.toml</c>。
/// </summary>
/// <remarks>
/// 通知中心、待办组件与日历组件共享同一实例，不要各自读文件。
/// </remarks>
public sealed class TodoDataSource : IDataSource<IReadOnlyList<TodoItem>>, IDisposable
{
    /// <summary>
    /// 数据源 Id。
    /// </summary>
    public const string SourceId = "todo";

    private const string TodosKey = "todos";
    private const string MetaVersionKey = "meta.version";

    private static readonly TimeSpan s_saveDebounce = TimeSpan.FromMilliseconds(500);

    private readonly Lock _gate = new();
    private readonly List<TodoItem> _items = [];
    private readonly string _path;
    private readonly Timer _saveTimer;
    private bool _dirty;
    private bool _disposed;

    /// <summary>
    /// 创建待办数据源。
    /// </summary>
    /// <param name="path">待办文件路径，通常为 <c>.lithe\todos.toml</c>。</param>
    public TodoDataSource(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
        _saveTimer = new Timer(_ => Save(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public string Id => SourceId;

    /// <inheritdoc />
    public IReadOnlyList<TodoItem> CurrentValue
    {
        get
        {
            lock (_gate)
            {
                return [.. _items.Select(static item => item with { })];
            }
        }
    }

    /// <inheritdoc />
    public DateTime LastUpdated { get; private set; } = DateTime.MinValue;

    /// <inheritdoc />
    /// <remarks>待办由事件驱动，此属性仅为满足接口，不产生定时刷新。</remarks>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.Zero;

    /// <inheritdoc />
    public event EventHandler<DataSourceUpdatedEventArgs<IReadOnlyList<TodoItem>>>? Updated;

    /// <inheritdoc />
    public Task StartAsync()
    {
        Load();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        Save();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 新增待办。
    /// </summary>
    public void Add(TodoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            _items.Add(item);
            MarkDirty();
        }

        RaiseUpdated();
    }

    /// <summary>
    /// 删除待办。
    /// </summary>
    /// <returns>是否删除了对应条目。</returns>
    public bool Remove(Guid id)
    {
        bool removed;
        lock (_gate)
        {
            removed = _items.RemoveAll(item => item.Id == id) > 0;
            if (removed)
            {
                MarkDirty();
            }
        }

        if (removed)
        {
            RaiseUpdated();
        }

        return removed;
    }

    /// <summary>
    /// 切换完成状态。重复待办完成后会顺延到下一个周期，而不是留在已完成列表里。
    /// </summary>
    public bool ToggleDone(Guid id)
    {
        TodoItem? changed = null;

        lock (_gate)
        {
            var index = _items.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return false;
            }

            var item = _items[index];
            if (item.Done)
            {
                item.Done = false;
                item.CompletedAt = null;
                _items[index] = item;
                changed = item;
            }
            else if (item.Recurrence == TodoRecurrence.None)
            {
                item.Done = true;
                item.CompletedAt = DateTimeOffset.Now;
                _items[index] = item;
                changed = item;
            }
            else
            {
                _items.RemoveAt(index);
                changed = item with
                {
                    Id = Guid.NewGuid(),
                    Done = false,
                    CompletedAt = null,
                    CreatedAt = DateTimeOffset.Now,
                    DueAt = NextDue(item),
                };
                _items.Add(changed);
            }

            MarkDirty();
        }

        RaiseUpdated();
        return true;
    }

    /// <summary>
    /// 修改待办内容。
    /// </summary>
    public bool Update(Guid id, Action<TodoItem> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        lock (_gate)
        {
            var index = _items.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return false;
            }

            var item = _items[index];
            mutate(item);
            _items[index] = item;
            MarkDirty();
        }

        RaiseUpdated();
        return true;
    }

    /// <summary>
    /// 清空已完成条目，返回清除数量。
    /// </summary>
    public int ClearCompleted()
    {
        int removed;
        lock (_gate)
        {
            removed = _items.RemoveAll(static item => item.Done);
            if (removed > 0)
            {
                MarkDirty();
            }
        }

        if (removed > 0)
        {
            RaiseUpdated();
        }

        return removed;
    }

    /// <summary>
    /// 立即写入文件。
    /// </summary>
    public void Save()
    {
        string snapshot;
        lock (_gate)
        {
            if (!_dirty && File.Exists(_path))
            {
                return;
            }

            snapshot = Serialize();
            _dirty = false;
        }

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _path + ".tmp";
            File.WriteAllText(temp, snapshot);
            File.Move(temp, _path, overwrite: true);
        }
        catch
        {
            lock (_gate)
            {
                _dirty = true;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _saveTimer.Dispose();
        Save();
    }

    private static DateTimeOffset? NextDue(TodoItem item)
    {
        if (item.DueAt is not { } due)
        {
            return null;
        }

        return item.Recurrence switch
        {
            TodoRecurrence.Daily => due.AddDays(1),
            TodoRecurrence.Weekly => due.AddDays(7),
            _ => due,
        };
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var table = TomlSerializer.Parse(File.ReadAllText(_path));
            var array = table.Find(TodosKey) as TomlArray;

            lock (_gate)
            {
                _items.Clear();
                if (array is not null)
                {
                    foreach (var node in array.Items)
                    {
                        if (node is TomlTable item && ReadItem(item) is { } todo)
                        {
                            _items.Add(todo);
                        }
                    }
                }
            }

            LastUpdated = DateTime.Now;
        }
        catch (Exception ex)
        {
            // 待办文件损坏时不能让应用起不来，保留空列表并留下诊断。
            AppLog.Warn($"待办文件 {_path} 读取失败，已忽略损坏内容", ex);
        }
    }

    private string Serialize()
    {
        var table = new TomlTable();
        table.SetPath(MetaVersionKey, TomlNode.Scalar(1));
        table.SetPath(TodosKey, new TomlArray([.. _items.Select(WriteItem)]));
        return TomlSerializer.Serialize(table);
    }

    private static TomlTable WriteItem(TodoItem item)
    {
        var node = new TomlTable();
        node.SetPath("id", TomlNode.Scalar(item.Id.ToString()));
        node.SetPath("title", TomlNode.Scalar(item.Title));

        if (!string.IsNullOrEmpty(item.Note))
        {
            node.SetPath("note", TomlNode.Scalar(item.Note));
        }

        node.SetPath("done", TomlNode.Scalar(item.Done));
        node.SetPath("createdAt", TomlNode.Scalar(item.CreatedAt));

        if (item.DueAt is { } due)
        {
            node.SetPath("dueAt", TomlNode.Scalar(due));
        }

        if (item.CompletedAt is { } completed)
        {
            node.SetPath("completedAt", TomlNode.Scalar(completed));
        }

        if (item.Recurrence != TodoRecurrence.None)
        {
            node.SetPath("recurrence", TomlNode.Scalar(item.Recurrence.ToString()));
        }

        return node;
    }

    private static TodoItem? ReadItem(TomlTable table)
    {
        if (table["title"] is not TomlScalar title)
        {
            return null;
        }

        var item = new TodoItem { Title = (string)title.Value };

        if (table["id"] is TomlScalar { Value: string idText } && Guid.TryParse(idText, out var id))
        {
            item = item with { Id = id };
        }

        if (table["note"] is TomlScalar note)
        {
            item.Note = (string)note.Value;
        }

        if (table["done"] is TomlScalar { Value: bool done })
        {
            item.Done = done;
        }

        if (table["createdAt"] is TomlScalar { Value: DateTimeOffset createdAt })
        {
            item = item with { CreatedAt = createdAt };
        }

        if (table["dueAt"] is TomlScalar { Value: DateTimeOffset dueAt })
        {
            item.DueAt = dueAt;
        }

        if (table["completedAt"] is TomlScalar { Value: DateTimeOffset completedAt })
        {
            item.CompletedAt = completedAt;
        }

        if (table["recurrence"] is TomlScalar { Value: string recurrence }
            && Enum.TryParse<TodoRecurrence>(recurrence, ignoreCase: true, out var parsed))
        {
            item.Recurrence = parsed;
        }

        return item;
    }

    private void MarkDirty()
    {
        _dirty = true;
        _saveTimer.Change(s_saveDebounce, Timeout.InfiniteTimeSpan);
    }

    private void RaiseUpdated()
    {
        LastUpdated = DateTime.Now;
        Updated?.Invoke(this, new DataSourceUpdatedEventArgs<IReadOnlyList<TodoItem>>(CurrentValue, LastUpdated));
    }
}