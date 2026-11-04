using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SonarController.Models;

namespace SonarController.Services.License;

public interface ILicenseProvider
{
    Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
