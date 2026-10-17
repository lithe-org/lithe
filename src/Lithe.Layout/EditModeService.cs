using Lithe.Core.Layout;

namespace Lithe.Layout;

/// <summary>
/// 编辑模式服务。桌面小组件的拖拽、缩放、删除都只在编辑模式内开放。
/// </summary>
public sealed class EditModeService : IEditModeService
{
    private readonly Lock _gate = new();
    private bool _isEditing;

    /// <inheritdoc />
    public bool IsEditing
    {
        get
        {
            lock (_gate)
            {
                return _isEditing;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? EditModeChanged;

    /// <inheritdoc />
    public void Enter() => SetEditing(true);

    /// <inheritdoc />
    public void Exit() => SetEditing(false);

    /// <inheritdoc />
    public void Toggle() => SetEditing(!IsEditing);

    private void SetEditing(bool value)
    {
        lock (_gate)
        {
            if (_isEditing == value)
            {
                return;
            }

            _isEditing = value;
        }

        EditModeChanged?.Invoke(this, EventArgs.Empty);
    }
}