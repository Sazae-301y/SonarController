using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SonarController.Models;

namespace SonarController.Services.License;

/// <summary>プロバイダー側でライセンス変更を検知できる場合に実装する。</summary>
public interface ILicenseChangeNotifier
{
    event EventHandler? LicenseChanged;
}

public interface ILicenseProvider
{
    Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
