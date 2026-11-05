namespace SonarController.Models;

public enum LicenseState
{
    Trial,
    Purchased,
    Expired,
    Unavailable,
    Unlicensed,
#if LICENSE_STORE_DEV
    Development,
#endif
}

/// <param name="RemainingDays">体験版のみ有効。それ以外は null</param>
public sealed record LicenseStatus(LicenseState State, int? RemainingDays = null, DateTimeOffset? ExpiresAt = null)
{
#if LICENSE_STORE_DEV
    public bool IsUsable => State is LicenseState.Trial or LicenseState.Purchased or LicenseState.Development;
#else
    public bool IsUsable => State is LicenseState.Trial or LicenseState.Purchased;
#endif

    /// <summary>製品版でない利用者に購入ページを案内する。状態が不明(Unavailable)の場合は案内しない</summary>
    public bool ShouldPromptPurchase => State is LicenseState.Trial or LicenseState.Expired or LicenseState.Unlicensed;

    /// <summary>ライセンスなし・期限切れは購入案内を強調表示する</summary>
    public bool IsPurchaseUrgent => State is LicenseState.Expired or LicenseState.Unlicensed;

    public static LicenseStatus Purchased() => new(LicenseState.Purchased);
    public static LicenseStatus Unavailable() => new(LicenseState.Unavailable);
}
