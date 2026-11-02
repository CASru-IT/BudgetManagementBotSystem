# Budget command consistency implementation plan

> **For agentic workers:** Use superpowers:executing-plans; execute inline and request one final review.

**Goal:** 英語の履歴・取消コマンドに統一し、取消対象を入力候補で選択可能にする。
**Architecture:** 既存UseCaseに認可済み候補検索を追加し、Discord AutocompleteHandlerを接続。既存履歴コマンドを改名し日本語別名を削除。
**Tech Stack:** .NET 10, Discord.Net, EF Core, xUnit.
**Spec:** ../specs/2026-11-03-budget-command-consistency.md

## Global Constraints

DB変更なし。権限・確定処理・ページングを維持。候補25件、ラベル100文字。検索後に件数制限。

## Review Focus

- 無権限・無効ユーザーへの候補漏えいを防ぐ。
- 支出/返金/取消済/残高不足を候補から除く。
- 過年度や古い履歴を検索可能にする。
- Discordの型・件数・文字数制限と旧名の移行案内。
- 本番登録から重複する旧コマンドを除く。

### Task 1: Candidates and command migration

- [ ] テストを先に追加: 候補検索・認可・不適格除外・検索後limit・数字のID文字列value・ラベル・公開スキーマ/旧名案内・不正ID拒否。
- [ ] targeted testの失敗を確認。
- [ ] CancelBudgetAdditionUseCase.GetCandidatesAsync(ulong,string)とBudgetTransactionAutocompleteHandlerを実装。budget-history/cancel-budgetに改名し、メニュー・案内・運用資料を更新。
- [ ] dotnet testで関連/既存テスト成功を確認、レビューと修正、commit/push。

### Task 2: Production rollout

- [ ] DLLを既存本番イメージへ組み込み、既存依存DLLの一致を確認。本番DBの複製でAdminの候補検索・schemaと既存データ保持を検証。
- [ ] Botを停止してDB/証憑/構成をバックアップし、本番へ反映。
- [ ] Ready/無再起動、Discord登録された新名・autocomplete・旧名の非存在を確認。
- [ ] 反映記録をcommit/push。
