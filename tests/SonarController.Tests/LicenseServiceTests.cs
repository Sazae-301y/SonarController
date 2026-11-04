using SonarController.Models;
using SonarController.Services.License;
using Xunit;

namespace SonarController.Tests;

public sealed class LicenseServiceTests
{
    private sealed class FakeProvider(LicenseStatus status) : ILicenseProvider, ILicenseChangeNotifier
    {
        public LicenseStatus Status { get; set; } = status;
        public int Calls { get; private set; }
        public event EventHandler? LicenseChanged;

        public Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Status);
        }

        public void RaiseChanged() => LicenseChanged?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void BeforeFirstRefresh_IsNotUsable()
    {
        var service = new LicenseService(new FakeProvider(LicenseStatus.Purchased()));

        Assert.False(service.IsUsable);
        Assert.Throws<LicenseAccessException>(service.EnsureUsable);
    }

    [Fact]
    public async Task Purchased_IsUsable()
    {
        var service = new LicenseService(new FakeProvider(LicenseStatus.Purchased()));

        await service.RefreshAsync();

        Assert.True(service.IsUsable);
        service.EnsureUsable();
    }

    [Fact]
    public async Task Trial_IsUsable_WhileNotExpired()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(3);
        var service = new LicenseService(new FakeProvider(new LicenseStatus(LicenseState.Trial, 3, expires)));

        await service.RefreshAsync();

        Assert.True(service.IsUsable);
    }

    [Fact]
    public async Task Trial_BecomesExpired_WhenExpirationPassesBetweenRefreshes()
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(-1);
        var service = new LicenseService(new FakeProvider(new LicenseStatus(LicenseState.Trial, 1, expires)));
        LicenseStatus? notified = null;
        service.StatusChanged += (_, s) => notified = s;

        await service.RefreshAsync();

        Assert.False(service.IsUsable);
        Assert.Equal(LicenseState.Expired, service.Current.State);
        Assert.Equal(LicenseState.Expired, notified?.State);
    }

    [Theory]
    [InlineData(LicenseState.Expired)]
    [InlineData(LicenseState.Unavailable)]
    public async Task ExpiredOrUnavailable_BlocksOperations(LicenseState state)
    {
        var service = new LicenseService(new FakeProvider(new LicenseStatus(state, 0, null)));
        await service.RefreshAsync();

        var ex = Assert.Throws<LicenseAccessException>(service.EnsureUsable);
        Assert.Equal(state, ex.State);
    }

    [Fact]
    public async Task StatusChanged_FiresOnlyWhenStatusChanges()
    {
        var provider = new FakeProvider(LicenseStatus.Purchased());
        var service = new LicenseService(provider);
        var count = 0;
        service.StatusChanged += (_, _) => count++;

        await service.RefreshAsync();
        await service.RefreshAsync();
        Assert.Equal(1, count);

        provider.Status = LicenseStatus.Unavailable();
        await service.RefreshAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ProviderChangeNotification_TriggersRefresh()
    {
        var provider = new FakeProvider(LicenseStatus.Purchased());
        var service = new LicenseService(provider);

        provider.RaiseChanged();
        for (var i = 0; i < 50 && provider.Calls == 0; i++)
            await Task.Delay(20);

        Assert.True(provider.Calls > 0);
        Assert.True(service.IsUsable);
    }

    [Fact]
    public async Task FullProvider_AlwaysPurchased()
    {
        var status = await new FullLicenseProvider().GetStatusAsync();

        Assert.Equal(LicenseState.Purchased, status.State);
        Assert.True(status.IsUsable);
    }
}
