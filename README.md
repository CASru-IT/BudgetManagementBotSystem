# BudgetManagementBotSystem

Discord 上で動作する予算管理 Bot システムです。  
現在はドメインロジックと一部ユースケース、Bot の最小動作（`/test`）までを実装しています。

## 現在の実装状況（2026-03 時点）

### 実装済み

- Worker 起動時に `Discord:Token` を読み取り、Discord Bot を起動
- スラッシュコマンドのグローバル登録
- `/test` コマンド（疎通確認）
- ドメイン層（`Group` / `User` / `BudgetRequest` / `BudgetTransaction` など）
- `SubmitBudgetRequestUseCase`
  - 申請作成
  - 予算上限チェック（不足時は `Rejected`）
- `IncreaseBudgetLimitUseCase`
  - 収入トランザクション追加による予算増額
- EF Core `BudgetManagementDbContext` とマッピング定義

### テスト実装済み

- `BudgetRequest` のステータス遷移ルール
- `SubmitBudgetRequestUseCase` の正常系・異常系
- `IncreaseBudgetLimitUseCase` の正常系・異常系

### 未実装 / 雛形段階

- `Infrastructure/Persistence/Repository/EfCoreGroupRepository.cs`（空ファイル）
- `IUserRepository` の EF Core 実装
- Discord 側の業務コマンド（`/test` 以外）
- DTOs / Queries の具体実装

## 必要な環境

- .NET 10 SDK
- PostgreSQL
- Discord Bot Token

## セットアップ

### 1. リポジトリ取得

```bash
git clone https://github.com/YOUR_USERNAME/BudgetManagementBotSystem.git
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
  "FiscalYearStartMonth": {
    "Month": 4
  }
}
```

> 現在の `Program.cs` では `AddDbContext(...UseNpgsql(...))` を実行しているため、`ConnectionStrings:Db` は実質必須です。

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

## 設計資料

- [設計資料](docs/design.md)

## プロジェクト構成

```text
BudgetManagementBotSystem/
├─ src/BudgetManagementBotSystem/
│  ├─ Application/
│  │  └─ UseCases/
│  ├─ Domain/
│  │  ├─ Entities/
│  │  ├─ Enums/
│  │  ├─ Repository/
│  │  ├─ Services/
│  │  └─ ValueObjects/
│  ├─ Infrastructure/
│  │  ├─ Discord/
│  │  └─ Persistence/
│  ├─ Presentation/Discord/Modules/
│  ├─ Program.cs
│  └─ Worker.cs
└─ tests/BudgetManagementBotSystem.Tests/
```

## 使用技術

- .NET 10
- Discord.Net 3.18.0
- Entity Framework Core 10.0.4
- Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1
- xUnit
- Moq

## ライセンス

MIT License
