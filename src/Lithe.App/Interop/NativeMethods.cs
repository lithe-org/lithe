using System.Runtime.InteropServices;

namespace Lithe_App.Interop;

/// <summary>
/// 窗口相关的 Win32 封装。后续 Dock、顶栏与桌面层共用。
/// </summary>
internal static partial class NativeMethods
{
    internal const int GWL_EXSTYLE = -20;
    internal const int GWL_STYLE = -16;

    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_CHILD = 0x40000000;

    internal const int HTTRANSPARENT = -1;
    internal const int HTCLIENT = 1;
    internal const int HTCAPTION = 2;

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_FRAMECHANGED = 0x0020;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    internal const uint SMTO_NORMAL = 0x0000;

    /// <summary>
    /// 读取窗口样式。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    /// <summary>
    /// 写入窗口样式。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    /// <summary>
    /// 设置父窗口。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetParent(nint hWndChild, nint hWndNewParent);

    /// <summary>
    /// 查窗口。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindow(string? lpClassName, string? lpWindowName);

    /// <summary>
    /// 按类名枚举窗口。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    /// <summary>
    /// 取窗口所属进程与类名信息。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumChildWindows(nint hWndParent, EnumWindowsProc lpEnumFunc, nint lParam);

    /// <summary>
    /// 窗口枚举回调。
    /// </summary>
    internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    /// <summary>
    /// 带超时的消息发送。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SendMessageTimeout(
        nint hWnd,
        uint msg,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeout,
        out nint result);

    /// <summary>
    /// 取窗口样式（32 位兼容入口）。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    internal static partial int GetWindowLong32(nint hWnd, int nIndex);

    /// <summary>
    /// 写窗口样式（32 位兼容入口）。
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    internal static partial int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    /// <summary>
    /// 读取扩展样式，自动适配 32/64 位。
    /// </summary>
    internal static nint GetExStyle(nint hWnd) =>
        nint.Size == 8 ? GetWindowLongPtr(hWnd, GWL_EXSTYLE) : GetWindowLong32(hWnd, GWL_EXSTYLE);

    /// <summary>
    /// 写入扩展样式，自动适配 32/64 位。
    /// </summary>
    internal static void SetExStyle(nint hWnd, nint style) =>
        _ = nint.Size == 8 ? SetWindowLongPtr(hWnd, GWL_EXSTYLE, style) : SetWindowLong32(hWnd, GWL_EXSTYLE, (int)style);

    /// <summary>
    /// 读取窗口样式，自动适配 32/64 位。
    /// </summary>
    internal static nint GetStyle(nint hWnd) =>
        nint.Size == 8 ? GetWindowLongPtr(hWnd, GWL_STYLE) : GetWindowLong32(hWnd, GWL_STYLE);

    /// <summary>
    /// 写入窗口样式，自动适配 32/64 位。
    /// </summary>
    internal static void SetStyle(nint hWnd, nint style) =>
        _ = nint.Size == 8 ? SetWindowLongPtr(hWnd, GWL_STYLE, style) : SetWindowLong32(hWnd, GWL_STYLE, (int)style);
}