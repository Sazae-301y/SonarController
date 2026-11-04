# バージョン管理

アプリのバージョンは [SonarController.csproj](./SonarController.csproj) の `<Version>` を唯一の管理元とします。設定画面の表示はこの値から生成され、`scripts/BuildFull.ps1` / `scripts/BuildTrial.ps1` は同じ値をインストーラーの表示とファイル名に渡します。

## 配布版とライセンス

- `Full`（既定）: Inno Setup EXE配布用の制限なし版です。
- `Trial`: Inno Setup EXE配布用のローカル15日体験版です。
- `Store`: Microsoft Storeの `StoreContext` でライセンスを確認するMSIX用です。Partner Centerで設定した体験ライセンスを正とし、ローカルでは体験期間を数えません。

Store版はMSIXパッケージIDがない状態ではライセンスを有効化せず、`Unavailable` としてSonar操作を停止します。Inno SetupのEXEインストーラーは `Full` / `Trial` 用であり、Store版をStoreライセンス付きEXEとして配布するものではありません。Store版を検証するには、Partner Centerに関連付けたMSIXをインストールし、Storeライセンスが取得できる環境で確認してください。

Store版のアプリ本体は次のように発行できます（生成物をMSIXとしてパッケージ化する工程は別途必要です）。

```powershell
dotnet publish SonarController.csproj -c Release -r win-x64 --self-contained true -p:LicenseEdition=Store
```

## バージョンの更新

`<Version>` は `MAJOR.MINOR.PATCH` 形式で管理します。

- **MAJOR**: 互換性のない変更
- **MINOR**: 既存機能と互換性のある機能追加
- **PATCH**: バグ修正や小さな改善

リリース時は `<Version>` を更新し、`scripts/BuildFull.ps1` / `scripts/BuildTrial.ps1` を実行してください。発行ファイルとインストーラーがそのバージョンで生成されます。今回の言語切替と設定画面の変更は `1.3.0` とします。
