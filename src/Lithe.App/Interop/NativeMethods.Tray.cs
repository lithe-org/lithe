using System.Runtime.InteropServices;

namespace Lithe_App.Interop;

/// <summary>
/// 托盘图标相关 Win32 封装。里程碑 4 的托盘消息代理完成前，
/// Lithe 自己的托盘图标先用这套最小实现保底。
/// </summary>
internal static partial class NativeMethods
{
    internal const uint WM_APP_TRAY = 0x8000 + 1;

    internal const uint NIM_ADD = 0;
    internal const uint NIM_DELETE = 2;

    internal const uint NIF_MESSAGE = 0x01;
    internal const uint NIF_ICON = 0x02;
    internal const uint NIF_TIP = 0x04;

    internal const uint WM_LBUTTONUP = 0x0202;
    internal const uint WM_RBUTTONUP = 0x0204;

    internal const uint MF_STRING = 0x0000;
    internal const uint TPM_RETURNCMD = 0x0100;
    internal const uint TPM_RIGHTBUTTON = 0x0002;

    internal const uint IMAGE_ICON = 1;
    internal const uint LR_LOADFROMFILE = 0x0010;
    internal const uint LR_DEFAULTSIZE = 0x0040;

    internal const int SwHide = 0;
    internal const int SwShow = 5;

    /// <summary>
    /// 添加、修改或删除托盘图标。
    /// </summary>
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShellNotifyIcon(uint dwMessage, in NotifyIconData lpdata);

    /// <summary>
    /// 从文件加载图标。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint LoadImage(nint hInst, string name, uint type, int cx, int cy, uint fuLoad);

    /// <summary>
    /// 加载系统默认图标，托盘图标文件缺失时兜底。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint LoadIcon(nint hInstance, nint lpIconName);

    /// <summary>
    /// 销毁图标句柄。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(nint hIcon);

    /// <summary>
    /// 创建弹出菜单。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint CreatePopupMenu();

    /// <summary>
    /// 追加菜单项。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AppendMenu(nint hMenu, uint uFlags, nint uIDNewItem, string lpNewItem);

    /// <summary>
    /// 在指定位置显示弹出菜单，TPM_RETURNCMD 下返回被点中的菜单项 Id。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint lprc);

    /// <summary>
    /// 销毁菜单。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyMenu(nint hMenu);

    /// <summary>
    /// 把窗口拉到前台。弹托盘菜单前必须调用，否则菜单无法正常关闭。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hWnd);
}

/// <summary>
/// NOTIFYICONDATAW。定长字符数组让结构体保持可 blittable，
/// 源生成 P/Invoke 才能直接传递。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NotifyIconData
{
    internal int cbSize;
    internal nint hWnd;
    internal uint uID;
    internal uint uFlags;
    internal uint uCallbackMessage;
    internal nint hIcon;
    internal fixed char szTip[128];
    internal int dwState;
    internal int dwStateMask;
    internal fixed char szInfo[256];

    /// <summary>uTimeout 与 uVersion 是联合体，这里只用到置零。</summary>
    internal uint uVersionOrTimeout;
    internal fixed char szInfoTitle[64];
    internal uint dwInfoFlags;
    internal fixed byte guidItem[16];
    internal nint hBalloonIcon;
}
