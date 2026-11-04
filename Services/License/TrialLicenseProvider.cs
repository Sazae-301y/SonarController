using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SonarController.Models;

namespace SonarController.Services.License;

/// <summary>
/// 初回起動日から一定期間のローカル体験管理（BOOTH体験版用）。
/// 開始日と最終確認日を複数の保存先（LocalAppData / HKCU / ProgramData）に
/// マシン固有キーのHMAC付きで保存し、最も古い開始日・最も新しい確認日を採用する。
/// 一部を削除しても残りから復元され、改ざん・時計の巻き戻しでも期間は延びない。
/// </summary>
public sealed class TrialLicenseProvider : ILicenseProvider
{
#if LICENSE_TRIAL_TEST
    public const int TrialDays = TrialTestSettings.TrialDays;

    private const string RegistryKeyPath = @"Software\SonarController\TrialTest";
    private const string RegistryValueName = "TrialTestState";
#else
    public const int TrialDays = 15;

    private const string RegistryKeyPath = @"Software\SonarController";
    private const string RegistryValueName = "TrialState";
#endif

    private static readonly TimeSpan WriteInterval = TimeSpan.FromHours(1);

    private readonly IReadOnlyList<IRecordStore> _stores;
    private readonly Func<DateTimeOffset> _now;
    private readonly byte[] _key;

    public TrialLicenseProvider()
        : this(
            [
                new FileStore(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#if LICENSE_TRIAL_TEST
                    "SonarController", "TrialTest", "trial.dat")),
#else
                    "SonarController", "trial.dat")),
#endif
                new RegistryStore(),
                new FileStore(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
#if LICENSE_TRIAL_TEST
                    "SonarController", "TrialTest", "trial.dat")),
#else
                    "SonarController", "trial.dat")),
#endif
            ],
            () => DateTimeOffset.UtcNow,
            GetMachineKey())
    {
    }

    internal TrialLicenseProvider(IReadOnlyList<IRecordStore> stores, Func<DateTimeOffset> now, byte[] key)
    {
        _stores = stores;
        _now = now;
        _key = key;
    }

    public Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = _now();
            var start = now;
            var lastSeen = now;
            var tampered = false;
            var anyFound = false;

            var parsed = new List<(DateTimeOffset Start, DateTimeOffset LastSeen)>();
            var missing = false;

            foreach (var store in _stores)
            {
                var raw = store.Read();
                if (raw is null)
                {
                    missing = true;
                    continue;
                }

                anyFound = true;
                if (TryParse(raw, out var s, out var l))
                {
                    parsed.Add((s, l));
                    if (s < start) start = s;
                    if (l > lastSeen) lastSeen = l;
                }
                else
                {
                    tampered = true;
                }
            }

            // 全保存先が一致していて確認日時が新しい場合は書き込みを省略する
            if (anyFound && !tampered && !missing && parsed.Count > 0)
            {
                var storedStart = parsed.Min(p => p.Start);
                var storedLast = parsed.Max(p => p.LastSeen);
                if (parsed.All(p => p.Start == storedStart && p.LastSeen == storedLast)
                    && now >= storedLast && now - storedLast < WriteInterval)
                {
                    return Task.FromResult(ToStatus(storedStart, now));
                }
            }

            // 改ざん・破損時は期限切れ扱い（再取得による延長を防ぐ）
            if (tampered)
            {
                start = DateTimeOffset.UnixEpoch;
                lastSeen = DateTimeOffset.UnixEpoch;
            }

            // 時計を戻された場合は、過去に観測した最新時刻を現在時刻として扱う
            var effectiveNow = now < lastSeen ? lastSeen : now;
            if (!anyFound) start = effectiveNow;

            var record = Serialize(start, effectiveNow);
            var saved = 0;
            foreach (var store in _stores)
            {
                if (store.TryWrite(record)) saved++;
            }

            if (saved == 0) return Task.FromResult(LicenseStatus.Unavailable());

            return Task.FromResult(ToStatus(start, effectiveNow));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Task.FromResult(LicenseStatus.Unavailable());
        }
    }

    private static LicenseStatus ToStatus(DateTimeOffset start, DateTimeOffset effectiveNow)
    {
        var expiresAt = start.AddDays(TrialDays);
        if (effectiveNow >= expiresAt)
            return new LicenseStatus(LicenseState.Expired, 0, expiresAt);

        var remaining = (int)Math.Ceiling((expiresAt - effectiveNow).TotalDays);
        return new LicenseStatus(LicenseState.Trial, remaining, expiresAt);
    }

    private string Serialize(DateTimeOffset start, DateTimeOffset lastSeen)
    {
        var payload = $"{start.ToUnixTimeSeconds()}|{lastSeen.ToUnixTimeSeconds()}";
        return $"{payload}|{Sign(payload)}";
    }

    private bool TryParse(string raw, out DateTimeOffset start, out DateTimeOffset lastSeen)
    {
        start = lastSeen = default;
        var parts = raw.Trim().Split('|');
        if (parts.Length != 3) return false;

        var payload = $"{parts[0]}|{parts[1]}";
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Sign(payload)), Encoding.UTF8.GetBytes(parts[2])))
            return false;

        if (!long.TryParse(parts[0], out var s) || !long.TryParse(parts[1], out var l)) return false;

        try
        {
            start = DateTimeOffset.FromUnixTimeSeconds(s);
            lastSeen = DateTimeOffset.FromUnixTimeSeconds(l);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private string Sign(string payload)
        => Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));

    // 他のPC・別環境からコピーした記録を無効にするため、マシン固有値を鍵に含める
    private static byte[] GetMachineKey()
    {
        string? guid = null;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            guid = key?.GetValue("MachineGuid") as string;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
        }

#if LICENSE_TRIAL_TEST
        return SHA256.HashData(Encoding.UTF8.GetBytes($"SonarController.TrialTest.v1|{guid ?? Environment.MachineName}"));
#else
        return SHA256.HashData(Encoding.UTF8.GetBytes($"SonarController.Trial.v1|{guid ?? Environment.MachineName}"));
#endif
    }

    internal interface IRecordStore
    {
        string? Read();
        bool TryWrite(string value);
    }

    private sealed class FileStore(string path) : IRecordStore
    {
        public string? Read()
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        public bool TryWrite(string value)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, value);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    private sealed class RegistryStore : IRecordStore
    {
        public string? Read()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
                return key?.GetValue(RegistryValueName) as string;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                return null;
            }
        }

        public bool TryWrite(string value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
                key.SetValue(RegistryValueName, value, RegistryValueKind.String);
                return true;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }
}
