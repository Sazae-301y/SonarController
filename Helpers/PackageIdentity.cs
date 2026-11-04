using System.Runtime.InteropServices;

namespace SonarController.Helpers;

/// <summary>MSIXパッケージ（パッケージID付き）として実行されているかを判定する。</summary>
public static class PackageIdentity
{
    private const int ErrorInsufficientBuffer = 122;

    private static readonly Lazy<(bool IsPackaged, int Error)> Cached = new(Query);

    public static bool IsPackaged => Cached.Value.IsPackaged;

    public static bool TryGet(out int error)
    {
        error = Cached.Value.Error;
        return Cached.Value.IsPackaged;
    }

    private static (bool, int) Query()
    {
        uint length = 0;
        var error = GetCurrentPackageFullName(ref length, IntPtr.Zero);
        return (error is 0 or ErrorInsufficientBuffer, error);
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
