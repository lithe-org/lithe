using Lithe_App.Diagnostics;
using Lithe_App.Interop;
using Microsoft.UI.Xaml;

namespace Lithe_App.Shell;

/// <summary>
/// 最小托盘图标：左键恢复设置页，右键菜单退出。
/// 关闭设置窗口只隐藏不退出，进程保活，桌面组件继续显示。
/// </summary>
/// <remarks>
/// 里程碑 4 会做系统托盘消息代理，接管第三方图标；这里只解决
/// "关掉控制台就找不到入口"的问题，不提前实现代理能力。
/// </remarks>
internal sealed partial class TrayIcon
{
    private const int SwHide = 0;
    private const int SwShow = 5;

    private const uint MenuIdShow = 1;
    private const uint MenuIdExit = 2;

    /// <summary>
    /// IDI_APPLICATION，托盘图标文件缺失时的兜底图标。
    /// </summary>
    private static readonly nint s_defaultIconId = new(32512);

    private static TrayIcon? s_instance;
    private static bool s_exiting;

    /// <summary>
    /// 子类化委托必须被强引用持有，否则被 GC 后托盘回调会静默失效。
    /// </summary>
    private static readonly NativeMethods.SubclassProc s_subclassCallback = SubclassCallback;

    private readonly Window _window;
    private readonly nint _hwnd;
    private readonly nint _icon;
    private bool _added;

    private TrayIcon(Window window, nint icon)
    {
        _window = window;
        _icon = icon;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
    }

    /// <summary>
    /// 是否正在退出。设置窗口据此决定放行关闭。
    /// </summary>
    public static bool IsExiting => s_exiting;

    /// <summary>
    /// 创建并显示托盘图标。失败只记日志，不影响主流程。
    /// </summary>
    public static TrayIcon? Initialize(Window window)
    {
        try
        {
            var icon = LoadTrayIcon();
            if (icon == 0)
            {
                ShellDiagnostics.Write("托盘图标加载失败，跳过托盘");
                return null;
            }

            var tray = new TrayIcon(window, icon);
            if (!tray.TryAdd())
            {
                ShellDiagnostics.Write("托盘图标注册失败，跳过托盘");
                _ = NativeMethods.DestroyIcon(icon);
                return null;
            }

            s_instance = tray;
            ShellDiagnostics.Write("托盘图标已就绪：左键恢复设置页，右键退出");
            return tray;
        }
        catch (Exception ex)
        {
            // 托盘是增强项，初始化失败不能让应用起不来。
            ShellDiagnostics.Write($"托盘图标初始化异常：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从托盘菜单退出：先摘图标再退出，避免留下幽灵托盘图标。
    /// </summary>
    public void RequestExit()
    {
        s_exiting = true;
        Dispose();
        Application.Current.Exit();
    }

    /// <summary>
    /// 恢复并激活设置窗口。
    /// </summary>
    public void RestoreWindow()
    {
        NativeMethods.ShowWindow(_hwnd, SwShow);
        _window.Activate();
    }

    /// <summary>
    /// 隐藏设置窗口（关闭按钮走这里，不退出进程）。
    /// </summary>
    public static void HideWindow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        NativeMethods.ShowWindow(hwnd, SwHide);
    }

    public unsafe void Dispose()
    {
        if (_added)
        {
            var data = new NotifyIconData
            {
                cbSize = sizeof(NotifyIconData),
                hWnd = _hwnd,
                uID = 1,
            };
            _ = NativeMethods.ShellNotifyIcon(NativeMethods.NIM_DELETE, data);
            _added = false;
        }

        _ = NativeMethods.RemoveWindowSubclass(_hwnd, s_subclassCallback, uIdSubclass: 1, dwRefData: 0);

        if (_icon != 0)
        {
            _ = NativeMethods.DestroyIcon(_icon);
        }

        if (ReferenceEquals(s_instance, this))
        {
            s_instance = null;
        }
    }

    private unsafe bool TryAdd()
    {
        _ = NativeMethods.SetWindowSubclass(_hwnd, s_subclassCallback, uIdSubclass: 1, dwRefData: 0);

        var data = new NotifyIconData
        {
            cbSize = sizeof(NotifyIconData),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_APP_TRAY,
            hIcon = _icon,
        };
        SetTip(ref data, "Lithe");

        _added = NativeMethods.ShellNotifyIcon(NativeMethods.NIM_ADD, data);
        return _added;
    }

    private static unsafe void SetTip(ref NotifyIconData data, string tip)
    {
        fixed (char* buffer = data.szTip)
        {
            var span = new Span<char>(buffer, 128);
            var length = Math.Min(tip.Length, span.Length - 1);
            tip.AsSpan(0, length).CopyTo(span);
            span[length] = '\0';
        }
    }

    private static nint LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(path))
        {
            var handle = NativeMethods.LoadImage(
                0, path, NativeMethods.IMAGE_ICON, 0, 0,
                NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);
            if (handle != 0)
            {
                return handle;
            }
        }

        return NativeMethods.LoadIcon(0, s_defaultIconId);
    }

    private void ShowContextMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        try
        {
            _ = NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, (nint)MenuIdShow, "显示设置");
            _ = NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, (nint)MenuIdExit, "退出");

            // 不先拉到前台，菜单点第一下只会关菜单不触发命令。
            _ = NativeMethods.SetForegroundWindow(_hwnd);

            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return;
            }

            var command = NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
                cursor.X, cursor.Y, 0, _hwnd, 0);

            if (command == MenuIdShow)
            {
                RestoreWindow();
            }
            else if (command == MenuIdExit)
            {
                RequestExit();
            }
        }
        finally
        {
            _ = NativeMethods.DestroyMenu(menu);
        }
    }

    private static nint SubclassCallback(nint hWnd, uint msg, nint wParam, nint lParam, nint uIdSubclass, nint dwRefData)
    {
        if (msg == NativeMethods.WM_APP_TRAY && s_instance is { } tray)
        {
            var mouseMessage = (uint)(lParam.ToInt64() & 0xFFFF);
            if (mouseMessage == NativeMethods.WM_LBUTTONUP)
            {
                tray.RestoreWindow();
            }
            else if (mouseMessage == NativeMethods.WM_RBUTTONUP)
            {
                tray.ShowContextMenu();
            }
        }

        return NativeMethods.DefSubclassProc(hWnd, msg, wParam, lParam);
    }
}
