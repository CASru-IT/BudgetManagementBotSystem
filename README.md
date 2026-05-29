# BudgetManagementBotSystem

Discord 上で動作する予算管理 Bot システムです。  
実装状況の詳細は docs を参照してください。

## ドキュメント

- [設計資料](docs/design.md)
- [実装状況](docs/implementation.md)
- [コマンド実装計画](docs/command-implementation-plan.md)

ドキュメント全体の目次は `docs/INDEX.md` を参照してください。

## 必要な環境

- .NET 10 SDK
- PostgreSQL
- Discord Bot Token

## セットアップ

### 1. リポジトリ取得

```bash
git clone https://github.com/CASru-IT/BudgetManagementBotSystem
cd BudgetManagementBotSystem
```

### 2. 依存パッケージ復元

```bash
dotnet restore
```

### 3. 設定ファイル作成

`src/BudgetManagementBotSystem/sample.appsettings.json` をコピーして  
`src/BudgetManagementBotSystem/appsettings.Development.json` を作成します。

PowerShell:

```powershell
Copy-Item src/BudgetManagementBotSystem/sample.appsettings.json src/BudgetManagementBotSystem/appsettings.Development.json
```

Bash:

```bash
cp src/BudgetManagementBotSystem/sample.appsettings.json src/BudgetManagementBotSystem/appsettings.Development.json
```

設定例:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Discord": {
    "Token": "YOUR_DISCORD_BOT_TOKEN"
  },
  "ConnectionStrings": {
    "Db": "Host=localhost;Database=budget;Username=postgres;Password=YOUR_PASSWORD"
  },
  "EvidenceStorage": {
    "BasePath": "data/evidences"
  },
  "FiscalYearStartMonth": {
    "Month": 4
  }
}
```

> 現在の `Program.cs` では `AddDbContext(...UseNpgsql(...))` を実行しているため、`ConnectionStrings:Db` は実質必須です。
> 証跡ファイル保存は `IFileStorage` の `LocalFileStorage` 実装を使用し、`EvidenceStorage:BasePath`（既定: `data/evidences`）配下に保存されます。

### 4. PostgreSQL データベース作成

```sql
CREATE DATABASE budget;
```

### 5. Discord Bot 作成

1. Discord Developer Portal でアプリケーション作成
2. Bot タブで Bot ユーザー作成
3. Token を `Discord:Token` に設定
4. OAuth2 URL Generator で Bot 招待 URL を生成してサーバーへ招待

## 実行方法

```bash
dotnet run --project src/BudgetManagementBotSystem/BudgetManagementBotSystem.csproj
```

## Docker Compose でのデプロイ

GitHub からクローンして Docker Compose で起動する場合は、次の手順でデプロイできます。

### 1. リポジトリ取得

```bash
git clone https://github.com/CASru-IT/BudgetManagementBotSystem.git
cd BudgetManagementBotSystem
```

### 2. 環境変数ファイルを作成

`.env.example` を `.env` にコピーして、必要な値を設定します。

```powershell
Copy-Item .env.example .env
```

設定する主な項目は次のとおりです。

- `Discord__Token`: Discord Bot のトークン
- `DB_USER`, `DB_PASSWORD`, `DB_NAME`: PostgreSQL の接続情報
- `AdminBootstrap__Password`: 初期管理者パスワード
- `EvidenceStorage__BasePath`: 証跡保存先パス

### 3. コンテナを起動

```bash
docker compose up -d --build
```

PostgreSQL と Bot が同時に起動します。`docker-compose.yml` では Bot コンテナが `postgres` に接続する構成になっています。

### 4. デプロイ後の確認

```bash
docker compose ps
docker compose logs -f app
```

正常に起動していれば、`app` コンテナのログに Bot の起動メッセージが出力されます。

### 5. 更新手順

リポジトリを更新したあと、再ビルドして再起動します。

```bash
git pull
docker compose up -d --build
```

停止する場合は次を実行します。

```bash
docker compose down
```

## テスト

```bash
dotnet test
```

## ライセンス

MIT License
