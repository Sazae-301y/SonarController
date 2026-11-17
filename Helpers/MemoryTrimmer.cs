using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace SonarController.Helpers;

/// <summary>常駐中にウィンドウが非表示のとき、マネージドヒープとワーキングセットを縮小する。</summary>
public static class MemoryTrimmer
{
    public static void Trim()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            using var process = Process.GetCurrentProcess();
            EmptyWorkingSet(process.Handle);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Memory trim failed.", ex);
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
}
