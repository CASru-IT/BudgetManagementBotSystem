# BudgetManagementBotSystem — ドキュメント目次

このフォルダ配下の主要ドキュメント一覧です。内容の整合性や参照関係はこの目次から確認してください。

## 使い分け

- [予算取消機能の更新手順](budget-cancellation-upgrade.md) — DB拡張・既存履歴・更新と切り戻し。
- [予算取消の設計](superpowers/specs/2026-11-02-cancel-budget-design.md) — 履歴指定の取消と競合対策。
- [予算コマンドの英語統一と入力候補](superpowers/specs/2026-11-03-budget-command-consistency.md) — 履歴統合・取消候補・旧名の移行。
- [2026-11-02 本番反映記録](deployments/2026-11-02-budget-cancellation.md) — 既存修正の維持、検証結果、バックアップと稼働版。

- [設計資料](design.md) — 全体構造、ユースケース設計、ドメインルールを確認する。
- [実装状況](implementation.md) — 現在の到達点と未実装項目を確認する。
- [運用資料](operations.md) — 実装済み Discord コマンドの運用手順と使い分けを確認する。
- [開発手順](development.md) — ローカル開発・テスト用のセットアップ手順を確認する。
- [コマンド実装計画](command-implementation-plan.md) — Discord コマンドの優先度と実装順序を確認する。
- [レイヤー依存関係図](layer-dependencies.md) — 層ごとの責務分離と例外を確認する。
- [コードリファクタ要約](code-refactor-summary.md) — 最近の構造変更と背景を確認する。
- [複数所属の設計](superpowers/specs/2026-10-31-multiple-groups-design.md) — 所属追加・解除と既存データの移行を確認する。
- [複数所属の更新手順](multiple-groups-upgrade.md) — 本番更新時のバックアップ・検証・切り戻しを確認する。

更新手順:

- 実装が進んだら、該当ドキュメントのステータスやリンクをこのファイルと各ドキュメントに反映してください。
