# Budget cancellation implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** 指定した追加履歴を確認付きで一度だけ取り消す。
**Architecture:** Groupが同年度の逆取引を生成。UseCaseが認可と保存、Discord moduleが確認セッションを管理。EFの班更新トークンと元取引IDの一意制約で競合を防止。
**Tech Stack:** .NET 10 / Discord.Net / EF Core / PostgreSQL / xUnit.
**Spec:** ../specs/2026-11-02-cancel-budget-design.md

## Global Constraints

元履歴は保持。理由1〜500字。確認10分。Admin / President / Accountantの有効ユーザーのみ。現在設定ではなく元の年度を使う。旧収入は由来未確認と表示。

## Review Focus

- 確認後の支出・別の取消との競合で負残高にならないこと。
- 無権限・無効化・第三者のボタン操作で更新できないこと。
- 旧収入の由来不明を明示し、既知の返金を取り消さないこと。
- 元年度の残高・未承認申請を検査すること。
- 既存DBへの列追加と一意制約が既存データを壊さないこと。

### Task 1: Domain, application and persistence

Files: Domain/Entities/{Group,BudgetTransaction}.cs, Domain/Enums/BudgetTransactionKind.cs, Application/UseCases/Budget/CancelBudgetAdditionUseCase.cs, Infrastructure/Persistence/{BudgetManagementDbContext,BudgetCancellationSchemaUpgrade}.cs, Program.cs (all src/BudgetManagementBotSystem); tests/.../BudgetCancellationTests.cs.

Interfaces: Group.ValidateBudgetCancellation(int) -> BudgetTransaction; Group.CancelBudgetAddition(int,int,string) -> BudgetTransaction; CancelBudgetAdditionUseCase.PreviewAsync(ulong,int,string) and ExecuteAsync(ulong,int,string,Guid) -> BudgetCancellationPreview.

- [x] Write failing tests for audit, no preview mutation, duplicate/expense/refund rejection, original fiscal year, spent/pending protection, authorization, concurrent saves and stale preview.
- [x] Run targeted tests: expected missing types initially.
- [x] Implement domain guards, application authorization, EF mapping/upgrade, revision increments on budget/requests changes; tag newly created income sources.
- [x] Run targeted tests; expect pass. Commit.

### Task 2: Discord confirmation and history

Files: Presentation/Discord/{Modules/BudgetCancellationModule,Services/BudgetCancellationConfirmationStore,Helpers/BudgetCancellationEmbeds}.cs; existing BudgetModule, DiscordEmbedFactory; Application/DTOs/TransactionDto.cs; BudgetQueryUseCase.cs; Program.cs; tests/.../BudgetCancellationPresentationTests.cs.

Interfaces: consumes Task 1 preview/execute; store.Create(owner, preview) -> token, store.TryTake(token,owner,out confirmation) -> bool; embeds show exact target/year/amount/reason and remaining balance.

- [x] Write tests for owner-bound atomic consumption, expiry, cancel-without-write, history IDs and reversal metadata, confirmation stale/permission checks.
- [x] Run targeted tests; expect missing feature failures.
- [x] Implement Japanese commands, deferred ephemeral replies, buttons with opaque tokens, clear errors; show audit in history and help.
- [x] Run targeted and full suite, update operations/design/implementation and DB upgrade procedure. Commit.

### Task 3: Review and delivery

- [x] Independent code review of changes against spec and Review Focus; resolve important findings with regression tests.
- [x] Run full suite and git diff --check. Expected all pass.
- [x] Commit and push feature branch as required by AGENTS.md; report commands, test results, deployment status.
