using SonarController.Models;
using SonarController.Services.License;
using Xunit;

namespace SonarController.Tests;

public sealed class StoreLicenseMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActiveFullLicense_IsPurchased()
    {
        var status = StoreLicenseProvider.ToLicenseStatus(true, false, DateTimeOffset.MaxValue, Now);

        Assert.Equal(LicenseState.Purchased, status.State);
    }

    [Fact]
    public void ActiveTrial_ReportsRemainingDays_RoundedUp()
    {
        var status = StoreLicenseProvider.ToLicenseStatus(true, true, Now.AddDays(12).AddHours(3), Now);

        Assert.Equal(LicenseState.Trial, status.State);
        Assert.Equal(13, status.RemainingDays);
    }

    [Fact]
    public void TrialPastExpiration_IsExpired()
    {
        var status = StoreLicenseProvider.ToLicenseStatus(true, true, Now.AddSeconds(-1), Now);

        Assert.Equal(LicenseState.Expired, status.State);
    }

    [Fact]
    public void InactiveLicense_IsExpired()
    {
        var status = StoreLicenseProvider.ToLicenseStatus(false, true, Now.AddDays(5), Now);

        Assert.Equal(LicenseState.Expired, status.State);
        Assert.False(status.IsUsable);
    }

    [Fact]
    public void UnixEpochExpiration_IsNotReportedAsDate()
    {
        var status = StoreLicenseProvider.ToLicenseStatus(false, false, DateTimeOffset.UnixEpoch, Now);

        Assert.Null(status.ExpiresAt);
    }
}
