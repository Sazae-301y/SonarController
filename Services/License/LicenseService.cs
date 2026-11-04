using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SonarController.Models;

namespace SonarController.Services.License;

/// <summary>ライセンス状態を保持し、変更を通知する。</summary>
public sealed class LicenseService
{
    private readonly ILicenseProvider _provider;

    public LicenseService(ILicenseProvider provider)
    {
        _provider = provider;
        if (provider is ILicenseChangeNotifier notifier)
        {
            notifier.LicenseChanged += (_, _) =>
            {
                // 通知はバックグラウンドスレッドから来るため、UIスレッドで状態を更新する
                if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
                    dispatcher.InvokeAsync(async () => await RefreshAsync());
                else
                    _ = RefreshAsync();
            };
        }
    }

    public LicenseStatus Current { get; private set; } = LicenseStatus.Unavailable();

    public bool IsUsable
    {
        get
        {
            ExpireTrialIfNecessary();
            return Current.IsUsable;
        }
    }

    public event EventHandler<LicenseStatus>? StatusChanged;

    public async Task<LicenseStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var status = await _provider.GetStatusAsync(cancellationToken);
        SetStatus(status);
        return Current;
    }

    public void EnsureUsable()
    {
        if (IsUsable) return;
        throw new LicenseAccessException(Current.State);
    }

    private void ExpireTrialIfNecessary()
    {
        if (Current.State == LicenseState.Trial
            && Current.ExpiresAt is { } expiresAt
            && DateTimeOffset.UtcNow >= expiresAt)
        {
            SetStatus(new LicenseStatus(LicenseState.Expired, 0, expiresAt));
        }
    }

    private void SetStatus(LicenseStatus status)
    {
        if (status != Current)
        {
            Current = status;
            StatusChanged?.Invoke(this, status);
        }
    }
}

public sealed class LicenseAccessException(LicenseState state)
    : InvalidOperationException($"Sonar operations are unavailable for license state '{state}'.")
{
    public LicenseState State { get; } = state;
}
