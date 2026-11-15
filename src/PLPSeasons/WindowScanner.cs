using System;
using System.Collections.Generic;
using System.Diagnostics;
using PLPSeasons.Core;

namespace PLPSeasons;

/// <summary>Finds the app windows snow can lie on (top-most first) and the taskbars, in physical screen pixels.</summary>
static class WindowScanner
{
    static readonly HashSet<string> Skip = new() { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland", "XamlExplorerHostIslandWindow", "ForegroundStaging", "MultitaskingViewFrame", "TaskListThumbnailWnd" };

    public static List<(long id, RectI rect)> AppWindows(ICollection<IntPtr> own)
    {
        var list = new List<(long, RectI)>();
        uint me = (uint)Environment.ProcessId;
        Native.EnumWindows((h, _) =>
        {
            if (own.Contains(h) || !Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
            Native.GetWindowThreadProcessId(h, out var pid); if (pid == me) return true;
            if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) return true;   // on another virtual desktop / hidden UWP
            long ex = (long)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE);
            if ((ex & Native.WS_EX_TOOLWINDOW) != 0 && (ex & Native.WS_EX_APPWINDOW) == 0) return true;
            if (Native.GetWindowTextLength(h) == 0) return true;
            if (Skip.Contains(Native.ClassOf(h))) return true;
            if (Native.DwmGetWindowAttribute(h, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out Native.RECT r, 16) != 0) Native.GetWindowRect(h, out r);
            var rect = new RectI(r.Left, r.Top, r.Right, r.Bottom);
            if (rect.Width < 120 || rect.Height < 60) return true;
            list.Add((h.ToInt64(), rect));
            return true;
        }, IntPtr.Zero);
        return list;     // EnumWindows walks top-most first
    }

    public static List<RectI> Taskbars()
    {
        var res = new List<RectI>();
        var main = Native.FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero && Native.GetWindowRect(main, out var r)) res.Add(new RectI(r.Left, r.Top, r.Right, r.Bottom));
        IntPtr sec = IntPtr.Zero;
        while ((sec = Native.FindWindowEx(IntPtr.Zero, sec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            if (Native.GetWindowRect(sec, out var r2)) res.Add(new RectI(r2.Left, r2.Top, r2.Right, r2.Bottom));
        return res;
    }

    public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;
}
