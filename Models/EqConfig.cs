namespace SonarController.Models;

/// <summary>
/// EQ設定プリセット（/configs エンドポイントから取得）
/// isFavorite == true のものがSonarでお気に入り登録済み
/// </summary>
public record EqConfig(
    string Id,
    string Name,
    SonarChannel Channel,
    bool IsFavorite,
    int FavoritePosition
);
