# 実装状況

## 関連ドキュメント

- [設計資料](design.md)

## 現在の実装状況（2026-03 時点）

### 実装済み

- Worker 起動時に `Discord:Token` を読み取り、Discord Bot を起動
- スラッシュコマンドのグローバル登録
- `/test` コマンド（疎通確認）
- ドメイン層（`Group` / `User` / `BudgetRequest` / `BudgetTransaction` など）
- `SubmitBudgetRequestUseCase`
  - 入力: `userId(int)`, `groupId(int)`, `amount(decimal)`, `description(string)`
  - 申請作成
  - 予算上限チェック（不足時は `Rejected`）
- `ApproveBudgetRequestUseCase`
  - 入力: `groupId(int)`, `requestId(int)`, `changedByUserId(int)`
  - 申請ステータスを `Approved` に更新
- `RejectBudgetRequestUseCase`
  - 入力: `groupId(int)`, `requestId(int)`, `changedByUserId(int)`
  - 申請ステータスを `Rejected` に更新
- `CancelBudgetRequestUseCase`
  - 入力: `groupId(int)`, `requestId(int)`, `changedByUserId(int)`
  - 申請ステータスを `ApprovalCancelled` に更新
- `IncreaseBudgetLimitUseCase`
  - 入力: `groupId(int)`, `amount(decimal)`
  - 収入トランザクション追加による予算増額
- EF Core `BudgetManagementDbContext` とマッピング定義
- `EfUnitOfWork`（`IUnitOfWork` 実装）

### テスト実装済み

- `BudgetRequest` のステータス遷移ルール
- `SubmitBudgetRequestUseCase` の正常系・異常系
- `ApproveBudgetRequestUseCase` の正常系・異常系
- `RejectBudgetRequestUseCase` の正常系・異常系
- `IncreaseBudgetLimitUseCase` の正常系・異常系
- `CancelBudgetRequestUseCase` の正常系・異常系

### 未実装 / 実装途中

- `Infrastructure/Persistence/EfCoreGroupRepository.cs`（メソッドはあるが `NotImplementedException`）
- `IUserRepository` の EF Core 実装
- Discord 側の業務コマンド（`/test` 以外）
- DTOs / Queries の具体実装

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
