# BudgetManagementBotSystem

Discord上で動作する予算管理Botシステム

## 必要な環境

- .NET 10.0 SDK
- PostgreSQL データベース
- Discord Bot Token

## セットアップ手順

### 1. リポジトリのクローン

```bash
git clone https://github.com/YOUR_USERNAME/BudgetManagementBotSystem.git
cd BudgetManagementBotSystem
```

### 2. 依存パッケージの復元

```bash
dotnet restore
```

### 3. PostgreSQLデータベースの準備

PostgreSQLをインストールし、データベースを作成します。

```sql
CREATE DATABASE budget;
```

### 4. 設定ファイルの作成

`src/BudgetManagementBotSystem/sample.appsettings.json`を参考に、`appsettings.Development.json`を作成します。

```bash
cd src/BudgetManagementBotSystem
cp sample.appsettings.json appsettings.Development.json
```

`appsettings.Development.json`を編集し、以下の設定を入力します：

- **Discord:Token**: Discord Developer PortalでBotを作成し、取得したトークンを設定
- **ConnectionStrings:Db**: PostgreSQLの接続文字列を設定

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
  }
}
```

### 5. Discord Botの作成

1. [Discord Developer Portal](https://discord.com/developers/applications)にアクセス
2. 「New Application」をクリックしてアプリケーションを作成
3. 「Bot」タブでBotユーザーを作成
4. 「Token」をコピーして、appsettings.Development.jsonに貼り付け
5. 「OAuth2」→「URL Generator」でBot招待URLを生成
   - Scopes: `bot`
   - Bot Permissions: 必要な権限を選択（メッセージの送信、読み取りなど）
6. 生成されたURLでBotをサーバーに招待

## 実行方法

### 開発環境での実行

プロジェクトディレクトリのルートから：

```bash
dotnet run --project src/BudgetManagementBotSystem/BudgetManagementBotSystem.csproj
```

または、プロジェクトフォルダ内で：

```bash
cd src/BudgetManagementBotSystem
dotnet run
```

### ビルドと実行

```bash
# ビルド
dotnet build

# リリースビルド
dotnet build -c Release

# リリース版の実行
dotnet run -c Release --project src/BudgetManagementBotSystem/BudgetManagementBotSystem.csproj
```

## テストの実行

```bash
dotnet test
```

## プロジェクト構成

```
BudgetManagementBotSystem/
├── src/
│   └── BudgetManagementBotSystem/      # メインプロジェクト
│       ├── Application/                # アプリケーション層
│       │   ├── DTO/                   # データ転送オブジェクト
│       │   └── Services/              # アプリケーションサービス
│       ├── Commands/                   # Discordコマンド
│       ├── Domain/                     # ドメイン層
│       │   ├── Entities/              # エンティティ
│       │   ├── Enums/                 # 列挙型
│       │   └── ValueObjects/          # 値オブジェクト
│       └── Infrastructure/             # インフラストラクチャ層
│           ├── Data/                  # データアクセス
│           └── Repository/            # リポジトリ
└── tests/
    └── BudgetManagementBotSystem.Tests/ # テストプロジェクト
```

## トラブルシューティング

### Botが起動しない場合

- Discord Tokenが正しく設定されているか確認
- PostgreSQLが起動しているか確認
- 接続文字列が正しいか確認

### コマンドが動作しない場合

- BotがサーバーにInviteされているか確認
- 必要な権限が付与されているか確認

## 使用技術

- .NET 10.0
- Discord.Net 3.18.0
- Entity Framework Core 10.0.3
- PostgreSQL (Npgsql 10.0.1)

## ライセンス

このプロジェクトのライセンスはMITライセンスです。
