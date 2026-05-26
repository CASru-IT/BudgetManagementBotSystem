# BudgetManagementBotSystem — コマンド実装計画

このドキュメントは Discord スラッシュコマンドの実装順序・依存関係・優先度を整理します。

## 関連ドキュメント

- [設計資料](design.md)
- [実装状況](implementation.md)

## 目的

この文書は、現時点で未実装または実装途中の Discord スラッシュコマンドについて、実装順序・依存関係・担当レイヤーを整理した計画書です。実装済みのコマンドは別途明示し、現状との差分が分かるようにしています。

## 現状認識

- `Presentation/Discord/Modules` にコマンド定義は存在している
- いくつかのコマンドは実処理まで接続済みだが、まだプレースホルダーのままのコマンドも多い
- 既存の実装済みユースケースは申請作成・承認・却下・取消・予算増額・班登録・ユーザー登録に集中している
- そのため、まずは「申請ワークフロー」と「予算参照系」を固め、その後に管理系・集計系・低優先度機能へ広げるのが効率的

## 実装状況サマリ

- 実装済みコマンド (Modules にて実装された Slash コマンド):
  - `/pending-list` (未承認一覧)
  - `/approve` (承認)
  - `/reject` (却下)
  - `/revoke-approval` (承認取消)
  - `/create-request` (申請作成)
  - `/list-requests` (申請一覧、ページング・フィルタ)
  - `/request-detail` (申請詳細表示)
  - `/cancel-request` (申請取消)
  - `/remaining-budget` (残予算表示)
  - `/usage-history` (予算取引履歴)
  - `/register-budget` (年度予算登録)
  - `/add-budget` (追加予算付与)
  - `/all-history` (全班の取引履歴)
  - `/register-group` (班登録)
  - `/list-groups` (班一覧、管理者限定)
  - `/register-user` (ユーザー登録)
  - `/set-user-role` (ユーザーの権限設定)
  - `/remove-user` (ユーザー無効化/削除)
  - `/list-users` (登録ユーザー一覧)
  - `/user-info` (ユーザー情報表示)
  - `/grant-role` (権限付与)
  - `/revoke-role` (権限解除)
  - `/assign-group` (班所属設定)
  - `/unassign-group` (班所属解除)
  - `/group-members` (班メンバー一覧)
  - `/become-admin` (管理者昇格 / システム初期化向け)

- モジュール定義済みだが未実装（プレースホルダ）のコマンド:
  - （現在 Modules によるプレースホルダは無し）

- 追加済みの基盤:
  - 認可ヘルパー (スタブ実装)
  - ページング用共通モデル (`PaginatedResult<T>`)
  - 申請・予算・ユーザー・班に共通する参照 DTO を追加
  - 失敗時レスポンスの共通化
  - 必要に応じて Repository の検索系メソッドを追加

上記は `Presentation/Discord/Modules` の実装と `Application/UseCases` への接続を含みます。

## コマンド一覧 (権限付き)

| コマンド | 権限 | 概要 |
| -------- | ---- | ---- |

`（一覧は Modules 実装に合わせて絞り込んでいます）`

| コマンド           | 権限               | 概要                               |
| ------------------ | ------------------ | ---------------------------------- |
| `/reject`          | 会計               | 指定した申請を却下する             |
| `/register-budget` | 会長               | 年度ごとの班予算を登録する         |
| `/add-budget`      | 会長               | 追加予算を付与する                 |
| `/register-group`  | 管理者             | 新しい班を登録する                 |
| `/list-groups`     | 管理者             | 登録済みの班一覧を表示する         |
| `/register-user`   | 管理者             | システム利用ユーザーを登録する     |
| `/grant-role`      | 管理者             | ユーザーへ権限を付与する           |
| `/revoke-role`     | 管理者             | ユーザーから権限を解除する         |
| `/assign-group`    | 管理者             | ユーザーを班へ所属させる           |
| `/cancel-request`  | 班長, 会長         | 確認待ち状態の申請を取り消す       |
| `/revoke-approval` | 会計               | 承認済み申請の承認を取り消す       |
| `/all-history`     | 会長, 会計         | 全班の予算使用履歴を閲覧する       |
| `/set-user-role`   | 管理者             | ユーザーの権限やロールを設定する   |
| `/remove-user`     | 管理者             | ユーザーを無効化または削除する     |
| `/list-users`      | 管理者             | 登録済みユーザーを表示する         |
| `/user-info`       | 管理者             | ユーザーの所属・権限情報を表示する |
| `/unassign-group`  | 管理者             | ユーザーの班所属を解除する         |
| `/group-members`   | 管理者, 会長       | 班ごとの所属メンバー一覧を表示する |
| `/delete-group`    | 管理者             | 班を削除または無効化する           |
| `/become-admin`    | 管理者（初期化）   | パスワードで自分を管理者に昇格する |

## 実装方針

1. 先に共通の土台を整える
   - 権限判定
   - ページング付き一覧応答
   - エラーメッセージの統一
   - DTO / Query モデルの整理

### 1-3 予算参照・登録系

| コマンド                       | 実装内容           | 主な依存                   |
| ------------------------------ | ------------------ | -------------------------- |
| `/remaining-budget` (実装済み) | 現在の残予算表示   | 予算集計 Query             |
| `/usage-history` (実装済み)    | 予算使用履歴の表示 | 履歴 Query, ページング     |
| `/register-budget`             | 年度予算の登録     | Budget 登録用 UseCase      |
| `/add-budget` (実装済み)       | 追加予算の登録     | IncreaseBudgetLimitUseCase |

### 1-4 管理系の基礎

| コマンド                     | 実装内容     | 主な依存                                    |
| ---------------------------- | ------------ | ------------------------------------------- |
| `/register-group` (実装済み) | 班登録       | RegisterGroupUseCase                        |
| `/list-groups` (実装済み)    | 班一覧表示   | IGroupRepository, IUserRepository, 認可判定 |
| `/register-user` (実装済み)  | ユーザー登録 | RegisterUserUseCase                         |
| `/grant-role`                | 権限付与     | Role 更新 UseCase                           |
| `/revoke-role`               | 権限解除     | Role 更新 UseCase                           |
| `/assign-group`              | 班所属の設定 | User 更新 UseCase                           |

## フェーズ 2: 中優先度コマンド

### 2-1 管理系の拡張

| コマンド          | 実装内容                   | 主な依存               |
| ----------------- | -------------------------- | ---------------------- |
| `/set-user-role`  | ユーザーの役割設定         | 権限モデルの整理       |
| `/remove-user`    | ユーザーの無効化または削除 | ユーザー状態管理       |
| `/list-users`     | 登録済みユーザー一覧       | User Query             |
| `/user-info`      | 所属・権限詳細の表示       | User Query, Group 参照 |
| `/unassign-group` | 班所属の解除               | User 更新 UseCase      |
| `/group-members`  | 班ごとの所属メンバー一覧   | Group Query            |

### 2-2 集計系

| コマンド       | 実装内容       | 主な依存             |
| -------------- | -------------- | -------------------- |
| `/all-history` | 全班の履歴閲覧 | 横断 Query           |

## フェーズ 3: 低優先度コマンド

| コマンド        | 実装内容             | 主な依存                     |
| --------------- | -------------------- | ---------------------------- |
| `/delete-group` | 班の削除または無効化 | 参照整合性、Soft Delete 方針 |

## 推奨実装順序

1. `/create-request` と `/list-requests` を実装する
2. `/pending-list`、`/approve`、`/reject` を実装する
3. `/remaining-budget` と `/usage-history` を実装する
4. `/register-budget`、`/add-budget` を実装する
5. `/register-group`、`/register-user`、`/grant-role`、`/assign-group` を整える
6. 集計 Query の整備を進める
7. 運用系（班削除の運用ポリシー策定など）を完了する

## コマンドごとの実装メモ

- 申請系は「作成」「一覧」「詳細」「状態変更」を分離する
- 承認系は状態遷移ルールと監査情報の永続化を優先する
- 予算系は年度の扱いを統一し、集計 Query を再利用する
- 管理系は CRUD だけでなく、無効化や所属解除の扱いを明確にする
- 集計系は専用 Query を用意し、Presentation 層に集計ロジックを置かない
## テスト計画

- Application 層のユースケース単体テストを先に追加する
- 状態遷移を伴うコマンドは正常系と異常系の両方を必須にする
- 一覧系はページング・フィルタ・空結果を確認する
- 権限制御があるコマンドは、権限あり・なしの両パターンを確認する

## 補足

この計画は、既存の実装済みユースケースを活かしながら、依存が小さいものから順番に積み上げる前提で書いています。実装が進んだら、この文書の各コマンド行を「未実装 / 実装途中 / 実装済み」に更新してください。
