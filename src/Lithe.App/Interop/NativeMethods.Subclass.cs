using System.Runtime.InteropServices;

namespace Lithe_App.Interop;

/// <summary>
/// 窗口子类化封装，用于接管 <c>WM_NCHITTEST</c> 等消息。
/// </summary>
internal static partial class NativeMethods
{
    internal const int WM_NCHITTEST = 0x0084;
    internal const int WM_NCDESTROY = 0x0082;

    internal const ushort GET_X_LPARAM = 0x0000;
    internal const ushort GET_Y_LPARAM = 0x0000;

    /// <summary>
    /// 子类化回调。
    /// </summary>
    internal delegate nint SubclassProc(nint hWnd, uint msg, nint wParam, nint lParam, nint uIdSubclass, nint dwRefData);

    /// <summary>
    /// 安装子类。
    /// </summary>
    [LibraryImport("comctl32.dll", EntryPoint = "SetWindowSubclass", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nint uIdSubclass, nint dwRefData);

    /// <summary>
    /// 卸载子类。
    /// </summary>
    [LibraryImport("comctl32.dll", EntryPoint = "RemoveWindowSubclass", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RemoveWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nint uIdSubclass, nint dwRefData);

    /// <summary>
    /// 调用原窗口过程。
    /// </summary>
    [LibraryImport("comctl32.dll", EntryPoint = "DefSubclassProc", SetLastError = true)]
    internal static partial nint DefSubclassProc(nint hWnd, uint msg, nint wParam, nint lParam);

    /// <summary>
    /// 取鼠标屏幕坐标。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point point);

    /// <summary>
    /// 屏幕坐标转客户区坐标。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ScreenToClient(nint hWnd, ref Point point);

    /// <summary>
    /// 客户端区屏幕坐标。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(nint hWnd, out Rect rect);
}

/// <summary>
/// Win32 坐标结构。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Point
{
    internal int X;

    internal int Y;
}

/// <summary>
/// Win32 矩形结构。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    internal Rect(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    internal int Left;

    internal int Top;

    internal int Right;

    internal int Bottom;
}