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

## テスト

```bash
dotnet test
```

## ライセンス

MIT License
