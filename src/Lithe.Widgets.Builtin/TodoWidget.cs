using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lithe.Core.Data;
using Lithe.Core.Diagnostics;
using Lithe.Core.Layout;
using Lithe.Data.Todo;
using Lithe.Widgets;
using Microsoft.UI.Xaml;

namespace Lithe.Widgets.Builtin;

/// <summary>
/// 待办组件。编辑模式外可以直接点击打卡，展开视图下可以直接添加。
/// </summary>
public sealed class TodoWidget : IWidgetFactory
{
    /// <summary>
    /// 组件 Id。
    /// </summary>
    public const string WidgetId = "todo";

    private readonly TodoDefinition _definition = new();

    /// <inheritdoc />
    public IWidget Definition => _definition;

    /// <inheritdoc />
    public FrameworkElement CreateView(WidgetViewMode mode, WidgetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var viewModel = new TodoWidgetViewModel(context);
        viewModel.LoadSettings();
        return new TodoWidgetView(viewModel);
    }

    private sealed class TodoDefinition : IWidget
    {
        public string Id => WidgetId;

        public string DisplayName => "待办";

        public WidgetSizeConstraint SizeConstraint { get; } = new(DefaultColSpan: 2, DefaultRowSpan: 3, MinColSpan: 2, MinRowSpan: 2, MaxColSpan: 4, MaxRowSpan: 6);

        public IReadOnlyList<string> DataSourceIds { get; } = [Lithe.Data.Todo.TodoDataSource.SourceId];
    }
}

/// <summary>
/// 待办组件 ViewModel。数据来自 <see cref="TodoDataSource"/>，写入后立即刷新。
/// </summary>
public sealed partial class TodoWidgetViewModel : ObservableObject, IDisposable, IWidgetViewAware
{
    private const int RegularVisibleItems = 5;

    private readonly WidgetContext _context;
    private readonly TodoDataSource? _todos;
    private WidgetSizeClass _sizeClass = WidgetSizeClass.Regular;
    private bool _showCompleted;

    [ObservableProperty]
    public partial IReadOnlyList<TodoItem> VisibleItems { get; set; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSummaryVisible { get; set; }

    [ObservableProperty]
    public partial bool IsAddRowVisible { get; set; }

    [ObservableProperty]
    public partial string NewTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EmptyText { get; set; } = "暂无待办";

    [ObservableProperty]
    public partial string OverdueHint { get; set; } = string.Empty;

    /// <summary>
    /// 创建待办 ViewModel。
    /// </summary>
    public TodoWidgetViewModel(WidgetContext context)
    {
        _context = context;

        _todos = context.GetDataSource<TodoDataSource>(Lithe.Data.Todo.TodoDataSource.SourceId);
        if (_todos is null)
        {
            AppLog.Warn("待办组件未找到待办数据源，组件将只读显示空列表");
            EmptyText = "待办数据源未就绪";
        }
        else
        {
            _todos.Updated += OnTodosUpdated;
            Refresh(_todos.CurrentValue);
        }
    }

    /// <summary>
    /// 读取配置项。
    /// </summary>
    public void LoadSettings()
    {
        _showCompleted = _context.GetSetting(TodoWidget.WidgetId, "show_completed", false);
        ApplySizeClass(_sizeClass);
    }

    /// <summary>
    /// 添加待办。
    /// </summary>
    [RelayCommand]
    public void Add()
    {
        if (_todos is null || string.IsNullOrWhiteSpace(NewTitle))
        {
            return;
        }

        _todos.Add(new TodoItem { Title = NewTitle.Trim() });
        NewTitle = string.Empty;
    }

    /// <summary>
    /// 打卡或取消打卡。
    /// </summary>
    [RelayCommand]
    public void Toggle(TodoItem? item)
    {
        if (_todos is null || item is null)
        {
            return;
        }

        _todos.ToggleDone(item.Id);
    }

    /// <summary>
    /// 删除待办。
    /// </summary>
    [RelayCommand]
    public void Remove(TodoItem? item)
    {
        if (_todos is null || item is null)
        {
            return;
        }

        _todos.Remove(item.Id);
    }

    /// <inheritdoc />
    public void OnSizeClassChanged(WidgetSizeClass sizeClass) => ApplySizeClass(sizeClass);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_todos is not null)
        {
            _todos.Updated -= OnTodosUpdated;
        }
    }

    private void ApplySizeClass(WidgetSizeClass sizeClass)
    {
        _sizeClass = sizeClass;

        IsSummaryVisible = sizeClass == WidgetSizeClass.Compact;
        IsAddRowVisible = sizeClass == WidgetSizeClass.Expanded;

        if (_todos is not null)
        {
            Refresh(_todos.CurrentValue);
        }
    }

    private void OnTodosUpdated(object? sender, DataSourceUpdatedEventArgs<IReadOnlyList<TodoItem>> args)
    {
        // 数据源在后台线程推送，UI 更新必须切回 UI 线程。
        _context.RunOnUiThread(() => Refresh(args.Value));
    }

    private void Refresh(IReadOnlyList<TodoItem> all)
    {
        var pending = all.Where(item => _showCompleted || !item.Done).ToList();

        var visible = _sizeClass == WidgetSizeClass.Compact
            ? []
            : _sizeClass == WidgetSizeClass.Regular
                ? pending.Take(RegularVisibleItems).ToList()
                : [.. pending];

        VisibleItems = visible;
        EmptyText = pending.Count == 0 ? "暂无待办" : string.Empty;

        var overdue = pending.Count(static item => item.IsOverdue);
        OverdueHint = overdue > 0 ? $"{overdue} 项已过期" : string.Empty;

        var latest = pending.FirstOrDefault();
        SummaryText = pending.Count == 0
            ? "暂无待办"
            : $"{pending.Count} 项待办{(latest is null ? string.Empty : $" · {latest.Title}")}";
    }
}