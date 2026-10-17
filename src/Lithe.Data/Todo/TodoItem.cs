namespace Lithe.Data.Todo;

/// <summary>
/// 待办重复规则。
/// </summary>
public enum TodoRecurrence
{
    /// <summary>
    /// 不重复。
    /// </summary>
    None,

    /// <summary>
    /// 每天。
    /// </summary>
    Daily,

    /// <summary>
    /// 每周。
    /// </summary>
    Weekly,
}

/// <summary>
/// 一条待办。
/// </summary>
public sealed record TodoItem
{
    /// <summary>
    /// 唯一标识。
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// 标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 备注。
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// 是否已完成。
    /// </summary>
    public bool Done { get; set; }

    /// <summary>
    /// 截止时间。
    /// </summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// 完成时间。
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// 重复规则。
    /// </summary>
    public TodoRecurrence Recurrence { get; set; } = TodoRecurrence.None;

    /// <summary>
    /// 是否已过期。
    /// </summary>
    public bool IsOverdue => !Done && DueAt is { } due && due < DateTimeOffset.Now;
}