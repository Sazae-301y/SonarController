using System.Diagnostics;
using System.Runtime.InteropServices;
using SonarController.Helpers;
using SonarController.Models;
using Windows.Services.Store;
using WinRT.Interop;

namespace SonarController.Services.License;

/// <summary>Microsoft StoreのMSIXアプリライセンスを確認する。</summary>
public sealed class StoreLicenseProvider : ILicenseProvider, ILicenseChangeNotifier
{
    public event EventHandler? LicenseChanged;

    private readonly Func<IntPtr> _getOwnerWindowHandle;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private StoreContext? _storeContext;

    private readonly ILicenseProvider _unpackagedFallback;

    // MSIXで実行されていない場合(EXE直接起動など)はローカル体験版として判定する
    public StoreLicenseProvider(Func<IntPtr> getOwnerWindowHandle, ILicenseProvider unpackagedFallback)
    {
        _getOwnerWindowHandle = getOwnerWindowHandle;
        _unpackagedFallback = unpackagedFallback;
    }

    public async Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (!PackageIdentity.TryGet(out var packageIdentityError))
            {
                Trace.TraceWarning("No package identity (error {0}); using local trial license.", packageIdentityError);
                return await _unpackagedFallback.GetStatusAsync(cancellationToken);
            }

            var context = _storeContext ??= CreateStoreContext();
            var license = await context.GetAppLicenseAsync().AsTask(cancellationToken);
            return ToLicenseStatus(license, DateTimeOffset.UtcNow);
        }
        catch (COMException ex)
        {
            Trace.TraceError("Microsoft Store license query failed: {0}", ex);
            return LicenseStatus.Unavailable();
        }
        catch (InvalidOperationException ex)
        {
            Trace.TraceError("Microsoft Store license query could not be initialized: {0}", ex);
            return LicenseStatus.Unavailable();
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace.TraceError("Microsoft Store license query was denied: {0}", ex);
            return LicenseStatus.Unavailable();
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private StoreContext CreateStoreContext()
    {
        var context = StoreContext.GetDefault();
        var ownerHandle = _getOwnerWindowHandle();
        if (ownerHandle == IntPtr.Zero)
            throw new InvalidOperationException("A valid WPF window handle is required for StoreContext.");

        InitializeWithWindow.Initialize(context, ownerHandle);
        context.OfflineLicensesChanged += (_, _) => LicenseChanged?.Invoke(this, EventArgs.Empty);
        return context;
    }

    private static LicenseStatus ToLicenseStatus(StoreAppLicense license, DateTimeOffset now)
        => ToLicenseStatus(license.IsActive, license.IsTrial, license.ExpirationDate, now);

    internal static LicenseStatus ToLicenseStatus(bool isActive, bool isTrial, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        DateTimeOffset? meaningfulExpiration =
            expiresAt > DateTimeOffset.UnixEpoch ? expiresAt : null;

        if (!isActive)
            return new LicenseStatus(LicenseState.Expired, 0, meaningfulExpiration);

        if (!isTrial)
            return LicenseStatus.Purchased();

        if (expiresAt <= now)
            return new LicenseStatus(LicenseState.Expired, 0, meaningfulExpiration);

        var remainingDays = (int)Math.Ceiling((expiresAt - now).TotalDays);
        return new LicenseStatus(LicenseState.Trial, remainingDays, meaningfulExpiration);
    }
}
