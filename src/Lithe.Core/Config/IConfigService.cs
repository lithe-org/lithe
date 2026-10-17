namespace Lithe.Core.Config;

/// <summary>
/// 配置读写服务。键为点分路径（如 <c>dock.height</c>），值支持字符串、整数、浮点、布尔与枚举。
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 读取配置。不存在或类型不匹配时抛出异常，由调用方决定默认值策略。
    /// </summary>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <param name="key">点分路径键。</param>
    T Get<T>(string key);

    /// <summary>
    /// 写入配置。写入后触发 500ms 防抖的自动保存。
    /// </summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="key">点分路径键。</param>
    /// <param name="value">值。</param>
    void Set<T>(string key, T value);

    /// <summary>
    /// 取配置值，键不存在时返回默认值。
    /// </summary>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <param name="key">点分路径键。</param>
    /// <param name="defaultValue">默认值。</param>
    T GetOrDefault<T>(string key, T defaultValue);

    /// <summary>
    /// 立即保存到磁盘。
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// 从磁盘加载。文件不存在时保留当前内容（首次启动即为空配置）。
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// 导出当前配置到指定路径。
    /// </summary>
    Task ExportAsync(string path);

    /// <summary>
    /// 从指定路径导入配置，替换当前内容。
    /// </summary>
    Task ImportAsync(string path);
}