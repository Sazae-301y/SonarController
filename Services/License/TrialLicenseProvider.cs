using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using SonarController.Models;

namespace SonarController.Services.License;

/// <summary>
/// 初回起動日から一定期間のローカル体験管理（BOOTH体験版用）。
/// 開始日と最終確認日をDPAPIで保護して保存し、時計の巻き戻しでは期間が延びないようにする。
/// </summary>
public sealed class TrialLicenseProvider : ILicenseProvider
{
    public const int TrialDays = 15;

    private readonly string _filePath;
    private readonly Func<DateTimeOffset> _now;

    public TrialLicenseProvider()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SonarController", "trial.dat"), () => DateTimeOffset.UtcNow)
    {
    }

    public TrialLicenseProvider(string filePath, Func<DateTimeOffset> now)
    {
        _filePath = filePath;
        _now = now;
    }

    public Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = _now();
            var (start, lastSeen) = Load() ?? (now, now);

            // 時計を戻された場合は、過去に観測した最新時刻を現在時刻として扱う
            var effectiveNow = now < lastSeen ? lastSeen : now;
            Save(start, effectiveNow);

            var expiresAt = start.AddDays(TrialDays);
            if (effectiveNow >= expiresAt)
                return Task.FromResult(new LicenseStatus(LicenseState.Expired, 0, expiresAt));

            var remaining = (int)Math.Ceiling((expiresAt - effectiveNow).TotalDays);
            return Task.FromResult(new LicenseStatus(LicenseState.Trial, remaining, expiresAt));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return Task.FromResult(LicenseStatus.Unavailable());
        }
    }

    private (DateTimeOffset Start, DateTimeOffset LastSeen)? Load()
    {
        if (!File.Exists(_filePath)) return null;

        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(_filePath), null, DataProtectionScope.CurrentUser);
            var parts = Encoding.UTF8.GetString(plain).Split('|');
            if (parts.Length == 2
                && long.TryParse(parts[0], out var s)
                && long.TryParse(parts[1], out var l))
            {
                return (DateTimeOffset.FromUnixTimeSeconds(s), DateTimeOffset.FromUnixTimeSeconds(l));
            }
        }
        catch (CryptographicException)
        {
        }

        // 改ざん・破損時は期限切れ扱い（再取得による延長を防ぐ）
        var epoch = DateTimeOffset.UnixEpoch;
        return (epoch, epoch);
    }

    private void Save(DateTimeOffset start, DateTimeOffset lastSeen)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var text = $"{start.ToUnixTimeSeconds()}|{lastSeen.ToUnixTimeSeconds()}";
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, data);
    }
}
