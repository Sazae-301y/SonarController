using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SonarController.Models;

namespace SonarController.Services.License;

/// <summary>ライセンス状態を保持し、変更を通知する。</summary>
public sealed class LicenseService
{
    private readonly ILicenseProvider _provider;

    public LicenseService(ILicenseProvider provider) => _provider = provider;

    public LicenseStatus Current { get; private set; } = LicenseStatus.Unavailable();

    public event EventHandler<LicenseStatus>? StatusChanged;

    public async Task<LicenseStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var status = await _provider.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status != Current)
        {
            Current = status;
            StatusChanged?.Invoke(this, status);
        }
        return status;
    }
}
