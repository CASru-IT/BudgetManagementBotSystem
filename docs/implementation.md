# BudgetManagementBotSystem — 実装状況

このドキュメントは現行実装（2026-04 時点）の状況をまとめます。

設計の前提や各ユースケースの処理フローは [設計資料](design.md) を、コマンドの優先度や実装順序は [コマンド実装計画](command-implementation-plan.md) を参照してください。

## 現在の実装状況（2026-04 時点）

### 実装済み

- Worker 起動時に `Discord:Token` を読み取り、Discord Bot を起動
- スラッシュコマンドのグローバル登録
- `/test` コマンド（疎通確認）
- `StartBudgetRequestWizard` コマンド（現状は案内メッセージ応答のみ）
- ドメイン層（`Group` / `User` / `BudgetRequest` / `BudgetTransaction` など）
- `SubmitBudgetRequestUseCase`
  - 入力: `userId(int)`, `groupId(int)`, `amount(decimal)`, `description(string)`, `evidenceFilePaths(IEnumerable<string>)`
  - 申請作成
  - 証跡ファイルパスを `BudgetRequest.Evidences` に反映
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
- `EfCoreGroupRepository`（`IGroupRepository` 実装）
- `EfCoreUserRepository`（`IUserRepository` 実装）
- `LocalFileStorage`（`IFileStorage` 実装）
  - `EvidenceStorage:BasePath`（既定: `data/evidences`）配下へ保存

注: Presentation 層の一部コマンドについて、スラッシュコマンド引数の受け取り方を改善しました。管理系コマンド（`/register-user` 等）は文字列での Discord ID 受け取りから、Discord のユーザー選択 (`IUser` 相当の `user` パラメータ) に変更されています。これによりコマンド UI 上で直接ユーザーを選べるようになり、`targetUser.Id` から Discord ID を取得します。

追記: 管理系の権限操作についてはコマンド整理を行い、`/grant-role` と `/revoke-role` を `/set-user-role` に統合しました。`/set-user-role` は管理者のみ実行可能となるよう、Presentation 層で権限チェックを導入しています。

### テスト実装済み

- `BudgetRequest` のステータス遷移ルール
- `SubmitBudgetRequestUseCase` の正常系・異常系
  - 証跡ファイルパス付き申請のテストを含む
- `ApproveBudgetRequestUseCase` の正常系・異常系
- `RejectBudgetRequestUseCase` の正常系・異常系
- `IncreaseBudgetLimitUseCase` の正常系・異常系
- `CancelBudgetRequestUseCase` の正常系・異常系

### 未実装 / 実装途中

- Discord 側の業務コマンド本実装（`StartBudgetRequestWizard` はプレースホルダー応答のみ）
- プレゼンテーション層からユースケース呼び出しまでの接続
- DTOs / Queries の具体実装
- ファイル保存のクラウド実装（現状はローカル保存のみ）
- 監査観点での申請ステータス変更者の永続化（`RequestStatusChange` への保持）

### DM 対応状況

現状の実装では、Bot は DM でも動作するように `DirectMessages` intent を有効化しています。

- DM で利用可能
  - `/create-request`  
    添付待ちを含むため `DirectMessages` intent が必要です。
  - `/list-requests`
  - `/request-detail`
  - `/cancel-request`
  - `/pending-list`
  - `/approve`
  - `/reject`
  - `/revoke-approval`
  - `/remaining-budget`
  - `/usage-history`
  - `/add-budget`
  - `/all-history`
  - `/become-admin`

- DM でもコード上は利用可能だが、Discord 側の UI 表示やユーザー選択の可否はクライアント仕様に依存する
  - `/register-user`
  - `/set-user-role`
  - `/remove-user`
  - `/user-info`
  - `/assign-group`
  - `/unassign-group`
  - `/group-members`

補足: DM では「班」の概念は維持されますが、チャンネルコンテキストがないため、添付ファイルの案内文や運用フローはサーバー内利用と比べてやや分かりにくくなります。

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
│  │  ├─ FileStorage/
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
