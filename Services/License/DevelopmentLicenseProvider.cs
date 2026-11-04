#if LICENSE_STORE_DEV
using SonarController.Models;

namespace SonarController.Services.License;

/// <summary>Allows full feature testing in development-only MSIX builds.</summary>
internal sealed class DevelopmentLicenseProvider : ILicenseProvider
{
    public Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new LicenseStatus(LicenseState.Development));
    }
}
#endif
