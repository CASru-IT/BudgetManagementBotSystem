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
- `UseInMemoryDatabase` を `true` に設定して、PostgreSQL を用意せずに起動可能にする
- `EvidenceStorage:BasePath` はローカル保存先（既定: `data/evidences`）

例:

```json
{
  "Discord": { "Token": "YOUR_DISCORD_BOT_TOKEN" },
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

## 補足

- 開発ではローカルの証跡保存フォルダ `src/BudgetManagementBotSystem/data/evidences` を利用します。必要に応じて消去や権限を調整してください。
- ディスコード関連の初期ユーザー登録や管理者付与は運用ドキュメントを参照してください。