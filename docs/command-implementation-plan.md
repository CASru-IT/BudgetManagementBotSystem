# コマンド実装計画

## 関連ドキュメント

- [設計資料](design.md)
- [実装状況](implementation.md)

## 目的

この文書は、現時点で未実装または実装途中の Discord スラッシュコマンドについて、実装順序・依存関係・担当レイヤーを整理した計画書です。

## 現状認識

- `Presentation/Discord/Modules` にコマンド定義は存在している
- ただし多くのコマンドはプレースホルダーで、Application / Domain / Infrastructure までの実処理が未接続
- 既存の実装済みユースケースは申請作成・承認・却下・取消・予算増額に集中している
- そのため、まずは「申請ワークフロー」と「予算参照系」を固め、その後に管理系・集計系・低優先度機能へ広げるのが効率的

## 実装状況サマリ

- 実装済みコマンド:
  - `/create-request` (申請作成)
  - `/list-requests` (申請一覧、ページング・フィルタ)
  - `/pending-list` (未承認一覧)
  - `/approve` (承認)
  - `/reject` (却下)
  - `/remaining-budget` (残予算表示)
  - `/usage-history` (予算取引履歴)
  - `/register-group` (班登録)
  - `/register-user` (ユーザー登録)

- 追加済みの基盤:
  - 認可ヘルパー (スタブ実装)
  - ページング用共通モデル (`PaginatedResult<T>`)
  - Application 層ユースケースの単体テスト（tests/\* に追加・既存テスト合計 33 件、全成功）

上記は `Presentation/Discord/Modules` の実装と `Application/UseCases` への接続を含みます。

## 実装方針

1. 先に共通の土台を整える
   - 権限判定
   - ページング付き一覧応答
   - エラーメッセージの統一
   - DTO / Query モデルの整理
2. 申請系と予算系を優先する
   - 日常運用で最も使う
   - 既存ユースケースとの接続がしやすい
3. 管理系は CRUD と監査を分離して実装する
   - ユーザー・班の管理操作は、読み取り系と更新系で依存が異なる
4. 集計・出力・検索は最後にまとめる
   - Query の追加や専用レポート処理が増えやすい

## フェーズ 0: 共通基盤の整備

### 実施項目

- Discord コマンド共通の認可ヘルパーの整理 (実装済み)
- ロールとコマンドの対応表の明文化
- 一覧表示用のページングモデルを追加 (実装済み)
- 申請・予算・ユーザー・班に共通する参照 DTO を追加
- 失敗時レスポンスの共通化
- 必要に応じて Repository の検索系メソッドを追加

### この段階で確認すること

- 役割ごとのアクセス制御が Presentation 層で一貫して判定できること
- 一覧系コマンドが後から同じ描画方式で実装できること

## フェーズ 1: 高優先度コマンド

### 1-1 申請作成・確認系

| コマンド                     | 実装内容                                     | 主な依存                                 |
| ---------------------------- | -------------------------------------------- | ---------------------------------------- |
| `/create-request` (実装済み) | 申請作成フロー、証跡添付、入力バリデーション | SubmitBudgetRequestUseCase, IFileStorage |
| `/list-requests` (実装済み)  | 班または役員会単位の申請一覧、ページング     | 申請検索 Query, Repository 参照拡張      |
| `/request-detail`            | 申請詳細、証跡、状態履歴の表示               | 申請詳細 Query, 状態履歴参照             |
| `/cancel-request`            | 確認待ち申請の取消                           | CancelBudgetRequestUseCase               |
| `/reapply`                   | 過去申請の複製と新規申請化                   | 申請詳細取得, SubmitBudgetRequestUseCase |
| `/expired-requests`          | 長期未処理申請の抽出                         | 申請一覧 Query, 日付条件検索             |
| `/officer-request`           | 役員会向けの申請作成                         | `/create-request` の派生実装             |

### 1-2 承認系

| コマンド                   | 実装内容                               | 主な依存                    |
| -------------------------- | -------------------------------------- | --------------------------- |
| `/pending-list` (実装済み) | 未承認申請の一覧、対象班ごとの絞り込み | 申請一覧 Query              |
| `/approve` (実装済み)      | 承認処理、対象申請の状態遷移           | ApproveBudgetRequestUseCase |
| `/reject` (実装済み)       | 却下処理、理由入力の扱い               | RejectBudgetRequestUseCase  |
| `/revoke-approval`         | 承認済み申請の承認取消                 | 状態遷移ルールの拡張        |
| `/finance-dashboard`       | 全班の申請・承認状況の集約表示         | Dashboard Query, 集計処理   |

### 1-3 予算参照・登録系

| コマンド                       | 実装内容               | 主な依存                     |
| ------------------------------ | ---------------------- | ---------------------------- |
| `/remaining-budget` (実装済み) | 現在の残予算表示       | 予算集計 Query               |
| `/usage-history` (実装済み)    | 予算使用履歴の表示     | 履歴 Query, ページング       |
| `/register-budget`             | 年度予算の登録         | Budget 登録用 UseCase        |
| `/add-budget`                  | 追加予算の登録         | IncreaseBudgetLimitUseCase   |
| `/change-budget`               | 登録済み予算の修正     | 予算更新 UseCase, 監査記録   |
| `/create-year`                 | 新年度の初期化         | 年度生成処理, 初期データ作成 |
| `/low-budget-warnings`         | 残予算が少ない班の抽出 | 予算集計 Query               |

### 1-4 管理系の基礎

| コマンド                     | 実装内容     | 主な依存             |
| ---------------------------- | ------------ | -------------------- |
| `/register-group` (実装済み) | 班登録       | RegisterGroupUseCase |
| `/register-user` (実装済み)  | ユーザー登録 | RegisterUserUseCase  |
| `/grant-role`                | 権限付与     | Role 更新 UseCase    |
| `/revoke-role`               | 権限解除     | Role 更新 UseCase    |
| `/assign-group`              | 班所属の設定 | User 更新 UseCase    |

## フェーズ 2: 中優先度コマンド

### 2-1 管理系の拡張

| コマンド          | 実装内容                   | 主な依存               |
| ----------------- | -------------------------- | ---------------------- |
| `/set-user-role`  | ユーザーの役割設定         | 権限モデルの整理       |
| `/settings`       | システム全体設定の変更     | 設定保存基盤           |
| `/audit-log`      | 操作履歴の確認             | 監査ログ保存, Query    |
| `/remove-user`    | ユーザーの無効化または削除 | ユーザー状態管理       |
| `/list-users`     | 登録済みユーザー一覧       | User Query             |
| `/user-info`      | 所属・権限詳細の表示       | User Query, Group 参照 |
| `/unassign-group` | 班所属の解除               | User 更新 UseCase      |
| `/group-members`  | 班ごとの所属メンバー一覧   | Group Query            |

### 2-2 集計・出力系

| コマンド           | 実装内容           | 主な依存             |
| ------------------ | ------------------ | -------------------- |
| `/monthly-summary` | 当月支出の集計表示 | 月次集計 Query       |
| `/all-history`     | 全班の履歴閲覧     | 横断 Query           |
| `/export-csv`      | CSV 出力           | CSV エクスポート処理 |

## フェーズ 3: 低優先度コマンド

| コマンド          | 実装内容                | 主な依存                     |
| ----------------- | ----------------------- | ---------------------------- |
| `/delete-group`   | 班の削除または無効化    | 参照整合性、Soft Delete 方針 |
| `/backup`         | DB / 設定のバックアップ | バックアップ実行基盤         |
| `/maintenance`    | メンテナンスモード切替  | システム状態管理             |
| `/budget-ranking` | 予算使用率ランキング    | 集計 Query                   |
| `/search-purpose` | 用途名検索              | 検索用 Index / Query         |

## 推奨実装順序

1. `/create-request` と `/list-requests` を実装する
2. `/pending-list`、`/approve`、`/reject` を実装する
3. `/remaining-budget` と `/usage-history` を実装する
4. `/register-budget`、`/add-budget`、`/change-budget` を実装する
5. `/register-group`、`/register-user`、`/grant-role`、`/assign-group` を整える
6. `/audit-log`、`/monthly-summary`、`/export-csv` などの集計・出力系へ進む
7. 最後に `/backup`、`/maintenance`、`/delete-group` のような運用系を実装する

## コマンドごとの実装メモ

- 申請系は「作成」「一覧」「詳細」「状態変更」を分離する
- 承認系は状態遷移ルールと監査情報の永続化を優先する
- 予算系は年度の扱いを統一し、集計 Query を再利用する
- 管理系は CRUD だけでなく、無効化や所属解除の扱いを明確にする
- 集計系は専用 Query を用意し、Presentation 層に集計ロジックを置かない
- 出力系は CSV スキーマを先に固定し、後から列追加しやすい形にする

## テスト計画

- Application 層のユースケース単体テストを先に追加する
- 状態遷移を伴うコマンドは正常系と異常系の両方を必須にする
- 一覧系はページング・フィルタ・空結果を確認する
- 権限制御があるコマンドは、権限あり・なしの両パターンを確認する
- 出力系は CSV の列順と内容を固定して検証する

## 補足

この計画は、既存の実装済みユースケースを活かしながら、依存が小さいものから順番に積み上げる前提で書いています。実装が進んだら、この文書の各コマンド行を「未実装 / 実装途中 / 実装済み」に更新してください。
