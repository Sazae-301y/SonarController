using SonarController.Models;
using SonarController.Services.License;
using Xunit;

namespace SonarController.Tests;

public sealed class TrialLicenseProviderTests
{
    private static readonly byte[] Key = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class MemoryStore : TrialLicenseProvider.IRecordStore
    {
        public string? Value { get; set; }
        public bool Writable { get; set; } = true;
        public int WriteCount { get; private set; }

        public string? Read() => Value;

        public bool TryWrite(string value)
        {
            if (!Writable) return false;
            Value = value;
            WriteCount++;
            return true;
        }
    }

    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = T0;
    }

    private static (TrialLicenseProvider Provider, MemoryStore[] Stores, Clock Clock) Create(byte[]? key = null, MemoryStore[]? stores = null, Clock? clock = null)
    {
        stores ??= [new MemoryStore(), new MemoryStore(), new MemoryStore()];
        clock ??= new Clock();
        return (new TrialLicenseProvider(stores, () => clock.Now, key ?? Key), stores, clock);
    }

    private static Task<LicenseStatus> Status(TrialLicenseProvider provider) => provider.GetStatusAsync();

    [Fact]
    public async Task FirstRun_StartsTrial_WithFullPeriod()
    {
        var (provider, stores, _) = Create();

        var status = await Status(provider);

        Assert.Equal(LicenseState.Trial, status.State);
        Assert.Equal(TrialLicenseProvider.TrialDays, status.RemainingDays);
        Assert.Equal(T0.AddDays(TrialLicenseProvider.TrialDays), status.ExpiresAt);
        Assert.All(stores, s => Assert.NotNull(s.Value));
    }

    [Fact]
    public async Task RemainingDays_DecreasesAsTimePasses()
    {
        var (provider, _, clock) = Create();
        await Status(provider);

        var elapsedDays = Math.Min(5, TrialLicenseProvider.TrialDays - 1);
        clock.Now = T0.AddDays(elapsedDays).AddHours(1);
        var status = await Status(provider);

        Assert.Equal(LicenseState.Trial, status.State);
        Assert.Equal(TrialLicenseProvider.TrialDays - elapsedDays, status.RemainingDays);
    }

    [Fact]
    public async Task Expires_AfterTrialPeriod()
    {
        var (provider, _, clock) = Create();
        await Status(provider);

        clock.Now = T0.AddDays(TrialLicenseProvider.TrialDays);
        var status = await Status(provider);

        Assert.Equal(LicenseState.Expired, status.State);
        Assert.False(status.IsUsable);
    }

    [Fact]
    public async Task ClockRollback_DoesNotExtendTrial()
    {
        var (provider, _, clock) = Create();
        await Status(provider);
        var observedDays = Math.Max(0, TrialLicenseProvider.TrialDays - 5);
        clock.Now = T0.AddDays(observedDays);
        await Status(provider);

        clock.Now = observedDays > 0 ? T0 : T0.AddHours(-1);
        var rolledBack = await Status(provider);
        Assert.Equal(TrialLicenseProvider.TrialDays - observedDays, rolledBack.RemainingDays);

        clock.Now = T0.AddDays(TrialLicenseProvider.TrialDays + 1);
        Assert.Equal(LicenseState.Expired, (await Status(provider)).State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DeletingOneStore_RestoresStartDate(int index)
    {
        var (provider, stores, clock) = Create();
        await Status(provider);
        var elapsedDays = Math.Min(8, TrialLicenseProvider.TrialDays - 1);
        clock.Now = T0.AddDays(elapsedDays);
        await Status(provider);

        stores[index].Value = null;
        var status = await Status(provider);

        Assert.Equal(LicenseState.Trial, status.State);
        Assert.Equal(TrialLicenseProvider.TrialDays - elapsedDays, status.RemainingDays);
        Assert.NotNull(stores[index].Value);
    }

    [Fact]
    public async Task DeletingTwoStores_StillKeepsOriginalStart()
    {
        var (provider, stores, clock) = Create();
        await Status(provider);
        clock.Now = T0.AddDays(20);
        stores[0].Value = null;
        stores[1].Value = null;

        Assert.Equal(LicenseState.Expired, (await Status(provider)).State);
    }

    [Fact]
    public async Task TamperedRecord_IsTreatedAsExpired()
    {
        var (provider, stores, _) = Create();
        await Status(provider);

        stores[1].Value = "9999999999|9999999999|DEADBEEF";

        Assert.Equal(LicenseState.Expired, (await Status(provider)).State);
    }

    [Fact]
    public async Task GarbageRecord_IsTreatedAsExpired()
    {
        var (provider, stores, _) = Create();
        await Status(provider);

        stores[0].Value = "not a record";

        Assert.Equal(LicenseState.Expired, (await Status(provider)).State);
    }

    [Fact]
    public async Task RecordFromAnotherMachine_IsRejected()
    {
        var (provider, stores, _) = Create();
        await Status(provider);

        var (otherMachine, _, _) = Create(key: [9, 9, 9, 9], stores: stores);

        Assert.Equal(LicenseState.Expired, (await Status(otherMachine)).State);
    }

    [Fact]
    public async Task WithinWriteInterval_DoesNotRewriteRecords()
    {
        var (provider, stores, clock) = Create();
        await Status(provider);
        var writes = stores.Sum(s => s.WriteCount);

        clock.Now = T0.AddMinutes(30);
        var status = await Status(provider);

        Assert.Equal(LicenseState.Trial, status.State);
        Assert.Equal(writes, stores.Sum(s => s.WriteCount));
    }

    [Fact]
    public async Task AfterWriteInterval_UpdatesLastSeen()
    {
        var (provider, stores, clock) = Create();
        await Status(provider);
        var writes = stores.Sum(s => s.WriteCount);

        clock.Now = T0.AddHours(2);
        await Status(provider);

        Assert.True(stores.Sum(s => s.WriteCount) > writes);
    }

    [Fact]
    public async Task MissingStore_IsRewrittenEvenWithinWriteInterval()
    {
        var (provider, stores, clock) = Create();
        await Status(provider);

        stores[2].Value = null;
        clock.Now = T0.AddMinutes(1);
        await Status(provider);

        Assert.NotNull(stores[2].Value);
    }

    [Fact]
    public async Task WhenNoStoreIsWritable_ReturnsUnavailable()
    {
        var stores = new[] { new MemoryStore { Writable = false }, new MemoryStore { Writable = false } };
        var (provider, _, _) = Create(stores: stores);

        Assert.Equal(LicenseState.Unavailable, (await Status(provider)).State);
    }

    [Fact]
    public async Task WhenSomeStoresAreWritable_StillWorks()
    {
        var stores = new[] { new MemoryStore { Writable = false }, new MemoryStore() };
        var (provider, _, _) = Create(stores: stores);

        Assert.Equal(LicenseState.Trial, (await Status(provider)).State);
    }
}
