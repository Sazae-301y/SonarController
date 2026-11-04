# バージョン管理

アプリのバージョンは [SonarController.csproj](./SonarController.csproj) の `<Version>` を唯一の管理元とします。設定画面の表示はこの値から生成され、`scripts/BuildFull.ps1` / `scripts/BuildTrial.ps1` は同じ値をインストーラーの表示とファイル名に渡します。

## バージョンの更新

`<Version>` は `MAJOR.MINOR.PATCH` 形式で管理します。

- **MAJOR**: 互換性のない変更
- **MINOR**: 既存機能と互換性のある機能追加
- **PATCH**: バグ修正や小さな改善

リリース時は `<Version>` を更新し、`scripts/BuildFull.ps1` / `scripts/BuildTrial.ps1` を実行してください。発行ファイルとインストーラーがそのバージョンで生成されます。今回の言語切替と設定画面の変更は `1.3.0` とします。
