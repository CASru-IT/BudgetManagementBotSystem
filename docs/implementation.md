# BudgetManagementBotSystem — 実装状況

このドキュメントは現行実装（2026-06 時点）の状況をまとめます。

設計の前提や各ユースケースの処理フローは [設計資料](design.md) を、コマンドの優先度や実装順序は [コマンド実装計画](command-implementation-plan.md) を参照してください。

## 現在の実装状況（2026-06 時点）

### 実装済み

- Worker 起動時に `Discord:Token` を読み取り、Discord Bot を起動
- スラッシュコマンドのグローバル登録
- `Program.cs` で既定カルチャを `ja-JP` に固定
- Docker 実行時の `LANG` / `LC_ALL` を `ja_JP.UTF-8` に固定
- ドメイン層（`Group` / `User` / `BudgetRequest` / `BudgetTransaction` など）
- `BudgetRequest.RequestDate`、`RequestStatusChange.ChangedAt`、`BudgetTransaction.TransactionDate` は `DateTime.UtcNow` で保存
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
- `/create-request`
  - Discord の添付引数として `evidence-1` から `evidence-5` を受け取る実装です。`evidence-1` は必須、追加証憑は任意です。
  - 証憑は jpg / jpeg / png / webp / pdf、1ファイル10MB以下に制限しています。
  - コマンド実行時点では申請を作成せず、確認 Embed と「申請する」「キャンセル」ボタンを表示します。
  - 確認データは `PendingRequestConfirmationStore` に10分間だけ保持し、「申請する」押下時に `SubmitBudgetRequestUseCase` を実行します。
  - 申請作成後の会計担当者 DM 通知は `DiscordRequestNotificationService` に分離しています。
- ユーザー管理コマンド
  - `/register-user` は Discord のユーザー選択から表示名を取得
  - `/set-user-name` で登録済みユーザーの表示名を変更
  - `/set-user-role` で登録済みユーザーの権限を変更
  - `/remove-user` でユーザーを無効化
  - `/activate-user` で無効化済みユーザーを再有効化

注: Presentation 層の一部コマンドについて、スラッシュコマンド引数の受け取り方を改善しました。管理系コマンド（`/register-user` 等）は文字列での Discord ID 受け取りから、Discord のユーザー選択 (`IUser` 相当の `user` パラメータ) に変更されています。これによりコマンド UI 上で直接ユーザーを選べるようになり、`targetUser.Id` から Discord ID を取得します。

追記: 管理系の権限操作についてはコマンド整理を行い、`/grant-role` と `/revoke-role` を `/set-user-role` に統合しました。`/set-user-role` は管理者のみ実行可能となるよう、Presentation 層で権限チェックを導入しています。

追記: `ApproveBudgetRequestUseCase` と承認通知 UseCase は残っていますが、現行の `ApprovalModule` では `/approve` スラッシュコマンドは公開されていません。承認操作を Discord から行う場合は、`ApprovalModule` への再追加が必要です。

### テスト実装済み

- `BudgetRequest` のステータス遷移ルール
- `SubmitBudgetRequestUseCase` の正常系・異常系
  - 証跡ファイルパス付き申請のテストを含む
- `ApproveBudgetRequestUseCase` の正常系・異常系
- `RejectBudgetRequestUseCase` の正常系・異常系
- `IncreaseBudgetLimitUseCase` の正常系・異常系
- `CancelBudgetRequestUseCase` の正常系・異常系

### 未実装 / 実装途中

- `/approve` スラッシュコマンドの再公開（UseCase は実装済み）
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
  - `/reject`
  - `/revoke-approval`
  - `/remaining-budget`
  - `/usage-history`
  - `/add-budget`
  - `/all-history`
  - `/become-admin`

- DM でもコード上は利用可能だが、Discord 側の UI 表示やユーザー選択の可否はクライアント仕様に依存する
  - `/register-user`
  - `/set-user-name`
  - `/set-user-role`
  - `/remove-user`
  - `/activate-user`
  - `/user-info`
  - `/assign-group`
  - `/unassign-group`
  - `/group-members`

補足: DM では「班」の概念は維持されますが、チャンネルコンテキストがないため、添付ファイルの案内文や運用フローはサーバー内利用と比べてやや分かりにくくなります。

## Discord コマンドの運用上の詳細

実務上の使い分けは [運用資料](operations.md) に分離しました。ここでは、実装済みコマンドの一覧と状態を確認してください。

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
