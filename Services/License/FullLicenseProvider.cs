using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SonarController.Models;

namespace SonarController.Services.License;

/// <summary>制限なしの完全版（知人配布用）。常に購入済み扱い。</summary>
public sealed class FullLicenseProvider : ILicenseProvider
{
    public Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LicenseStatus.Purchased());
}
