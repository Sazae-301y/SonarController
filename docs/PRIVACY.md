# プライバシーポリシー / Privacy Policy

SonarController は、個人情報を収集・送信しません。

## 保存されるデータ（すべてローカル）
- 設定ファイル: `%LocalAppData%\SonarController\*.json`（ホットキー、表示先、言語など）
- ログ: `%LocalAppData%\SonarController\logs\`（ユーザー名・PC名・ユーザープロファイルのパスはマスクされます）
- 体験版の記録: 体験開始日時（`trial.dat`、レジストリ `HKCU\Software\SonarController`、`%ProgramData%\SonarController`）

## 通信
- SteelSeries Sonar のローカル API（同一PC内）のみと通信します。
- Microsoft Store 版は、ライセンス確認のため Microsoft Store の API を利用します。
- 外部サーバーへの送信は行いません。

## 削除方法
アンインストール時に設定の削除を選択できます。体験版の記録は、体験期間の再取得防止のため残ります。
