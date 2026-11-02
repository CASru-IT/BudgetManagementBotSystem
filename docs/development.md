# 開発・ローカル実行手順

このドキュメントはローカル開発やデバッグ目的でのセットアップ手順をまとめます。デプロイ（本番運用）手順は README の「本番デプロイ（Ubuntu + Docker Compose）」を参照してください。

## 必要な環境（開発用）

- .NET 10 SDK
- Discord Bot Token

## セットアップ（ローカル）

1. リポジトリ取得

```bash
git clone https://github.com/CASru-IT/BudgetManagementBotSystem.git
cd BudgetManagementBotSystem
```

2. 依存パッケージ復元

```bash
dotnet restore
```

3. 設定ファイル作成

```bash
cp src/BudgetManagementBotSystem/sample.appsettings.json src/BudgetManagementBotSystem/appsettings.Development.json
```

appsettings のポイント（開発環境）:

- `Discord:Token` に Bot トークンを設定
- `Discord:CommandRegistrationMode` は通常 `Guild` にし、`Discord:GuildId` に開発用サーバー ID を設定
- `UseInMemoryDatabase` を `true` に設定して、PostgreSQL を用意せずに起動可能にする
- `EvidenceStorage:BasePath` はローカル保存先（既定: `data/evidences`）

例:

```json
{
  "Discord": {
    "Token": "YOUR_DISCORD_BOT_TOKEN",
    "CommandRegistrationMode": "Guild",
    "GuildId": 123456789012345678
  },
  "UseInMemoryDatabase": true,
  "EvidenceStorage": { "BasePath": "data/evidences" }
}
```

4. 実行

```bash
dotnet run --project src/BudgetManagementBotSystem/BudgetManagementBotSystem.csproj
```

5. テスト

```bash
dotnet test
```

複数所属の PostgreSQL 統合テストは、空の使い捨てデータベース（名前の末尾が `_test`）を用意し、接続文字列を `BUDGET_TEST_DB` に設定して `dotnet test` を実行します。この変数がない場合、その1件はスキップされます。既存テーブルがあるDBではテストが失敗するため、再実行時も新しいテストDBを用意してください。本番の接続情報は使用しません。

テストでは旧単一所属形式のデータ移行、移行再実行、全所属の読込、班追加・指定解除、両班への申請、重複防止、財務データ保持を確認します。Discordへの通信や実際の申請は行いません。

## 補足

- 開発ではローカルの証跡保存フォルダ `src/BudgetManagementBotSystem/data/evidences` を利用します。必要に応じて消去や権限を調整してください。
- アプリケーションは起動時に既定カルチャを `ja-JP` に固定します。Docker 実行時も `LANG=ja_JP.UTF-8` / `LC_ALL=ja_JP.UTF-8` を設定しています。
- ドメイン上の申請日時・状態変更日時・予算取引日時は UTC で保存します。表示や通知文言は日本語カルチャ前提です。
- ディスコード関連の初期ユーザー登録や管理者付与は運用ドキュメントを参照してください。
