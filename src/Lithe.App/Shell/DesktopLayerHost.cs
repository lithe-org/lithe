using System.Runtime.InteropServices;
using System.Text;
using Lithe_App.Interop;

namespace Lithe_App.Shell;

/// <summary>
/// 桌面层窗口宿主查找：定位 Explorer 的 WorkerW 层，把 Lithe 桌面层插到壁纸与图标之间。
/// </summary>
internal static class DesktopLayerHost
{
    private const string WorkerWClass = "WorkerW";
    private const string DesktopListViewClass = "SHELLDLL_DefView";

/// <summary>
/// 查找可作为桌面层宿主的窗口。
/// </summary>
/// <remarks>
/// 只做本地枚举与查找，不向 Explorer 发 <c>0x052C</c> 消息：
/// 实测该消息在 UI 线程会让 WinUI 以 <c>0xc000027b</c> 崩溃，在后台线程则长时间不返回。
/// <para>
/// 优先 <c>Progman</c> 下的 <c>SHELLDLL_DefView</c>（桌面图标层）：owner 挂到它，
/// 窗口即位于图标之上、普通窗口之下；取不到再退回 <c>Progman</c> 本身，
/// 最后才是带 <c>SHELLDLL_DefView</c> 的 <c>WorkerW</c>（实测当前系统 WorkerW
/// 全部不可见，只作兜底）。
/// </para>
/// </remarks>
public static nint FindDesktopHost()
{
    var progman = NativeMethods.FindWindow("Progman", null);
    if (progman != 0)
    {
        var defView = FindChildByClass(progman, DesktopListViewClass);
        return defView != 0 ? defView : progman;
    }

    nint firstWorker = 0;
    nint workerWithListView = 0;

    NativeMethods.EnumWindows((hwnd, _) =>
    {
        if (!string.Equals(GetClassName(hwnd), WorkerWClass, StringComparison.Ordinal))
        {
            return true;
        }

        firstWorker = hwnd;

        if (HasDesktopListView(hwnd))
        {
            workerWithListView = hwnd;
            return false;
        }

        return true;
    }, 0);

    return workerWithListView != 0 ? workerWithListView : firstWorker;
}

/// <summary>
/// 把窗口挂到桌面宿主之下（owner 方案）。
/// </summary>
/// <remarks>
/// 用 <c>SetWindowLongPtr(GWLP_HWNDPARENT)</c> 把宿主设为 owner，不改
/// <c>WS_CHILD</c> 风格，也不发跨进程同步消息；挂载后再
/// <c>SetWindowPos(HWND_BOTTOM)</c> 垫底。实测 <c>SetParent</c> 到 Progman
/// 会触发 USER32 同步消息风暴加 WinUI 内容岛屿重挂接：主窗口并存、桌面层
/// 已渲染时 UI 线程调用必崩（<c>0xc000027b</c>，Microsoft.UI.Xaml.dll），
/// 空闲时调用则死锁；owner 方案无此问题（DeskBox 同款做法）。
/// </remarks>
/// <param name="hwnd">桌面层窗口句柄。</param>
/// <param name="host">宿主窗口句柄。</param>
/// <returns>是否挂载成功。</returns>
public static bool Attach(nint hwnd, nint host)
{
    if (hwnd == 0 || host == 0)
    {
        return false;
    }

    NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_HWNDPARENT, host);

    // 垫到非置顶窗口底部：作为 owner 附属始终位于宿主（桌面图标层）之上、
    // 其他窗口之下，正是"所有窗口下方、桌面之上"。
    NativeMethods.SetWindowPos(
        hwnd,
        NativeMethods.HWND_BOTTOM,
        0, 0, 0, 0,
        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

    // 回读验证：SetWindowLongPtr 成功时返回原值，原值为 0 时无法区分成败。
    return NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWLP_HWNDPARENT) == host;
}

/// <summary>
/// 解除 owner 挂载，恢复独立顶层窗口。
/// </summary>
public static void Detach(nint hwnd)
{
    if (hwnd != 0)
    {
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_HWNDPARENT, 0);
    }
}

private static bool HasDesktopListView(nint hwnd) => FindChildByClass(hwnd, DesktopListViewClass) != 0;

private static nint FindChildByClass(nint parent, string className)
{
    nint found = 0;

    NativeMethods.EnumChildWindows(parent, (child, _) =>
    {
        if (string.Equals(GetClassName(child), className, StringComparison.Ordinal))
        {
            found = child;
            return false;
        }

        return true;
    }, 0);

    return found;
}

    private static string GetClassName(nint hwnd)
    {
        var builder = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }
}