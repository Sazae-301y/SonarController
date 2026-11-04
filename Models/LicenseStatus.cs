namespace SonarController.Models;

public enum LicenseState
{
    Trial,
    Purchased,
    Expired,
    Unavailable,
}

/// <param name="RemainingDays">体験版のみ有効。それ以外は null</param>
public sealed record LicenseStatus(LicenseState State, int? RemainingDays = null, DateTimeOffset? ExpiresAt = null)
{
    public bool IsUsable => State is LicenseState.Trial or LicenseState.Purchased;

    public static LicenseStatus Purchased() => new(LicenseState.Purchased);
    public static LicenseStatus Unavailable() => new(LicenseState.Unavailable);
}
