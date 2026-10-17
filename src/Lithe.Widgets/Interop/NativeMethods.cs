using System.Runtime.InteropServices;

namespace Lithe.Widgets;

/// <summary>
/// 组件交互需要的少量 Win32 封装。与桌面层交互相关的声明在宿主程序里。
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// 取鼠标屏幕坐标。返回物理像素，与网格指标单位一致。
    /// </summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point point);
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
