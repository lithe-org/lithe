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
/// 查找可作为桌面层父窗口的宿主。
/// </summary>
/// <remarks>
/// 只做本地枚举与查找，不向 Explorer 发 <c>0x052C</c> 消息：
/// 实测该消息在 UI 线程会让 WinUI 以 <c>0xc000027b</c> 崩溃，在后台线程则长时间不返回。
/// <para>
/// 优先用 <c>Progman</c>（程序管理器，实测是可见的桌面根窗口）；
/// 回退到带 <c>SHELLDLL_DefView</c> 的 <c>WorkerW</c>。实测当前系统上的 WorkerW 全部
/// <c>IsWindowVisible=false</c>，挂上去窗口会消失，所以只能作为最后兜底。
/// </para>
/// </remarks>
public static nint FindDesktopHost()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman != 0)
        {
            return progman;
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
    /// 把窗口挂到 WorkerW 下并调整为铺满该层。
    /// </summary>
    /// <param name="hwnd">桌面层窗口句柄。</param>
    /// <param name="worker">父窗口句柄。</param>
    /// <returns>是否挂载成功。</returns>
    public static bool Attach(nint hwnd, nint worker)
    {
        if (hwnd == 0 || worker == 0)
        {
            return false;
        }

        if (!NativeMethods.SetParent(hwnd, worker))
        {
            return false;
        }

        var style = NativeMethods.GetStyle(hwnd);
        NativeMethods.SetStyle(hwnd, (style & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD);

        return NativeMethods.GetParent(hwnd) == worker;
    }

    private static bool HasDesktopListView(nint hwnd)
    {
        var found = false;

        NativeMethods.EnumChildWindows(hwnd, (child, _) =>
        {
            if (string.Equals(GetClassName(child), DesktopListViewClass, StringComparison.Ordinal))
            {
                found = true;
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