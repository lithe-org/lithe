using System.Globalization;
using Lithe.Config.Toml;
using Lithe.Core.Config;
using Lithe.Core.Diagnostics;

namespace Lithe.Config;

/// <summary>
/// 基于 TOML 文件的配置服务。
/// </summary>
/// <remarks>
/// 写入采用「临时文件 + 替换」，避免写盘中途崩溃留下半截配置；
/// <see cref="Set{T}"/> 只标记脏数据，由 500ms 防抖统一落盘。
/// 读取带容错：主配置损坏时隔离为 <c>.corrupt-&lt;时间戳&gt;</c>，依次尝试
/// <c>.bak</c> 备份与默认值，损坏不再拖垮启动；保存成功后 best-effort
/// 更新 <c>.bak</c>。
/// </remarks>
public sealed class TomlConfigService : IConfigService, IDisposable
{
    /// <summary>
    /// 配置变更后的防抖间隔。
    /// </summary>
    public static readonly TimeSpan SaveDebounceDelay = TimeSpan.FromMilliseconds(500);

    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Timer _debounceTimer;
    private readonly string _path;
    private TomlTable _root;
    private bool _dirty;
    private bool _disposed;

    /// <summary>
    /// 创建配置服务。
    /// </summary>
    /// <param name="path">配置文件完整路径。</param>
    /// <param name="defaults">配置文件不存在时使用的默认内容。</param>
    public TomlConfigService(string path, TomlTable defaults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(defaults);

        _path = path;
        _root = defaults;
        _debounceTimer = new Timer(_ => _ = SaveAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// 配置文件路径。
    /// </summary>
    public string Path => _path;

    /// <inheritdoc />
    public T Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            if (_root.Find(key) is not TomlScalar scalar)
            {
                throw new KeyNotFoundException($"配置项 \"{key}\" 不存在。");
            }

            return ConvertScalar<T>(scalar, key);
        }
    }

    /// <inheritdoc />
    public T GetOrDefault<T>(string key, T defaultValue)
    {
        try
        {
            return Get<T>(key);
        }
        catch (KeyNotFoundException)
        {
            return defaultValue;
        }
        catch (InvalidCastException)
        {
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            _root.SetPath(key, TomlNode.FromValue(value));
            _dirty = true;
        }

        _debounceTimer.Change(SaveDebounceDelay, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public async Task SaveAsync()
    {
        string snapshot;
        lock (_gate)
        {
            if (!_dirty && File.Exists(_path))
            {
                return;
            }

            snapshot = TomlSerializer.Serialize(_root);
            _dirty = false;
        }

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _path + ".tmp";
            await File.WriteAllTextAsync(temp, snapshot).ConfigureAwait(false);
            File.Move(temp, _path, overwrite: true);

            // 主配置落盘后同步刷新备份，供损坏恢复使用；失败只记日志。
            UpdateBackup(snapshot);
        }
        catch
        {
            lock (_gate)
            {
                _dirty = true;
            }

            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task LoadAsync()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var text = await File.ReadAllTextAsync(_path).ConfigureAwait(false);

        try
        {
            var loaded = TomlSerializer.Parse(text);

            lock (_gate)
            {
                _root = loaded;
                _dirty = false;
            }
        }
        catch (Exception ex)
        {
            // 配置损坏不能拖垮启动：隔离坏文件 → 尝试备份 → 保留默认。
            AppLog.Warn($"配置文件解析失败：{_path}", ex);
            RecoverFromCorruption();
        }
    }

    /// <summary>
    /// 配置损坏后的恢复流程：先把坏文件隔离成 <c>.corrupt-&lt;时间戳&gt;</c>，
    /// 再尝试解析 <c>.bak</c>；两者都不可用时保留默认值。
    /// </summary>
    /// <remarks>
    /// 从备份或默认恢复时标记脏数据，由下一次保存重建主配置；隔离失败
    /// （文件被占用等）时保留磁盘现场，不覆盖用户数据，等人工处理。
    /// </remarks>
    private void RecoverFromCorruption()
    {
        var isolated = TryIsolateCorruptFile();

        var backup = TryParseBackup();
        lock (_gate)
        {
            if (backup is not null)
            {
                _root = backup;
                _dirty = true;
                AppLog.Warn($"已从备份恢复配置：{_path}.bak");
                return;
            }

            AppLog.Warn("配置备份不可用，本次运行使用默认配置");
            _dirty = isolated;
        }
    }

    /// <summary>
    /// 把损坏的配置重命名为带时间戳的隔离文件，返回是否成功。
    /// </summary>
    private bool TryIsolateCorruptFile()
    {
        try
        {
            var target = $"{_path}.corrupt-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}";
            File.Move(_path, target);
            AppLog.Warn($"损坏的配置已隔离为 {target}");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warn("损坏配置隔离失败，保留原文件现场", ex);
            return false;
        }
    }

    /// <summary>
    /// 尝试解析备份配置，失败返回 <see langword="null"/>。
    /// </summary>
    private TomlTable? TryParseBackup()
    {
        var backupPath = _path + ".bak";
        if (!File.Exists(backupPath))
        {
            return null;
        }

        try
        {
            return TomlSerializer.Parse(File.ReadAllText(backupPath));
        }
        catch (Exception ex)
        {
            AppLog.Warn($"备份配置解析失败：{backupPath}", ex);
            return null;
        }
    }

    /// <summary>
    /// best-effort 刷新备份配置，失败不影响主流程。
    /// </summary>
    private void UpdateBackup(string snapshot)
    {
        try
        {
            File.WriteAllText(_path + ".bak", snapshot);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"配置备份更新失败：{_path}.bak", ex);
        }
    }

    /// <inheritdoc />
    public async Task ExportAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string snapshot;
        lock (_gate)
        {
            snapshot = TomlSerializer.Serialize(_root);
        }

        await File.WriteAllTextAsync(path, snapshot).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ImportAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var imported = TomlSerializer.Parse(text);

        lock (_gate)
        {
            _root = imported;
            _dirty = true;
        }

        // 导入是用户的显式操作，立即落盘，不等防抖。
        await SaveAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 取当前配置的快照副本，主要用于诊断与测试。
    /// </summary>
    public TomlTable Snapshot()
    {
        lock (_gate)
        {
            return _root;
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
        _debounceTimer.Dispose();
        _writeLock.Dispose();
    }

    private static T ConvertScalar<T>(TomlScalar scalar, string key)
    {
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var value = scalar.Value;

        if (target.IsInstanceOfType(value))
        {
            return (T)value;
        }

        try
        {
            if (target == typeof(string))
            {
                return (T)(object)Convert.ToString(value, CultureInfo.InvariantCulture)!;
            }

            if (target.IsEnum)
            {
                return value is string name
                    ? (T)Enum.Parse(target, name, ignoreCase: true)
                    : (T)Enum.ToObject(target, Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }

            if (target == typeof(DateTimeOffset))
            {
                return (T)(object)(value is DateTimeOffset offset ? offset : DateTimeOffset.Parse(ToText(value), CultureInfo.InvariantCulture));
            }

            if (target == typeof(DateTime))
            {
                return (T)(object)(value is DateTimeOffset offset ? offset.UtcDateTime : DateTime.Parse(ToText(value), CultureInfo.InvariantCulture));
            }

            if (target == typeof(bool))
            {
                // 只有 TOML 里本来就是布尔值时才允许，避免 64 -> true 这类误读。
                throw new InvalidCastException($"配置项 \"{key}\" 不是布尔值。");
            }

            if (value is not (long or double))
            {
                throw new InvalidCastException($"配置项 \"{key}\" 的值无法转换为 {target.Name}。");
            }

            return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidCastException($"配置项 \"{key}\" 的值 \"{scalar}\" 无法转换为 {target.Name}。", ex);
        }
    }

    private static string ToText(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}