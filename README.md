# BudgetManagementBotSystem

軽量な説明: Discord 上で動作する予算管理 Bot システムです。

開発・ローカル実行手順は [docs/development.md](docs/development.md) を参照してください。実装・設計の詳細は `docs/` 配下の各資料を参照してください。

## 軽いコマンド一覧

以下は実装済みの主要な Discord スラッシュコマンドの軽い一覧です。運用上の詳細や権限、使い方は [運用資料](docs/operations.md) を参照してください。

- システム
  - `/become-admin` — 初期管理者化（ブートストラップ用パスワード）
- 申請ワークフロー
  - `/create-request` — 予算使用申請の作成（証跡添付あり）
  - `/list-requests` — 自分の申請一覧表示
  - `/request-detail` — 指定申請の詳細表示（証跡添付を含む）
  - `/cancel-request` — 申請の取消
- 承認関連
  - `/pending-list` — 未承認申請一覧の表示
  - `/approve` — 申請の承認
  - `/reject` — 申請の却下
  - `/revoke-approval` — 承認の取り消し
- 予算関連
  - `/remaining-budget` — 班の残予算確認
  - `/usage-history` — 取引履歴表示
  - `/add-budget` — 追加予算の付与（管理者権限）
- 班・ユーザー管理
  - `/register-group`, `/delete-group`, `/list-groups`
  - `/register-user`, `/set-user-role`, `/remove-user`
  - `/assign-group`, `/unassign-group`, `/group-members`, `/list-users`, `/user-info`

詳しい実装状況は [実装状況](docs/implementation.md) を参照してください。

## 本番デプロイ（Ubuntu + Docker Compose）

Ubuntu 環境で Docker Compose を使って本番稼働させる手順を示します。

## 必要な環境

- OS: Ubuntu 22.04 LTS 推奨（20.04 でも可）
- Docker: Docker Engine 20.10 以上

### 1. リポジトリ取得

```bash
git clone https://github.com/CASru-IT/BudgetManagementBotSystem.git
cd BudgetManagementBotSystem
```

### 2. 環境変数ファイル作成

`.env.example` をコピーして `.env` を作成し、シークレット値を設定します。

```bash
cp .env.example .env
```

主な設定項目:

環境変数の詳細（`.env`）:

- `Discord__Token` (必須)
  - Discord の Bot トークンを指定します。公開しないでください。
  - 例: `Discord__Token=NzA1...`（実際の値は長いトークン文字列）

- `DB_USER`, `DB_PASSWORD`, `DB_NAME` (推奨)
  - Compose のデフォルト設定ではこれらの値から接続文字列を生成します（`ConnectionStrings__Db` が未指定の場合）。
  - 例: `DB_USER=postgres` / `DB_PASSWORD=secret` / `DB_NAME=budget`

- `ConnectionStrings__Db` (任意、上書き可)
  - 完全な Npgsql 接続文字列を直接指定する場合に使用します。設定すると上記の `DB_*` 値より優先して使用されます。
  - 例: `ConnectionStrings__Db=Host=postgres;Database=budget;Username=postgres;Password=secret`

- `AdminBootstrap__Password` (必須/初期化用)
  - `/become-admin` コマンドで初期管理者を立てるための共有パスワードです。

- `EvidenceStorage__BasePath` (必須)
  - コンテナ内の証跡ファイル保存先パス。デフォルトは `/app/data/evidences`。
  - `docker-compose.yml` ではホストの `./src/BudgetManagementBotSystem/data/evidences` をマウントしています。ホスト側のディレクトリに書き込み権限が必要です。

- `FiscalYearStartMonth__Month` (推奨)
  - 会計年度の開始月です。`1` から `12` の範囲で指定します。
  - 例: `FiscalYearStartMonth__Month=4`

- `UseInMemoryDatabase` (true|false)
  - 開発時に `true` にすると InMemory DB を使用して PostgreSQL を不要にします。本番では必ず `false` にしてください。

### 3. コンテナ起動

```bash
docker compose up -d --build
```

### 4. 起動確認

```bash
docker compose ps
docker compose logs -f app
```

### 5. 更新手順

```bash
git pull
docker compose up -d --build
```

停止:

```bash
docker compose down
```

## CasruBudgetDB ボリュームの中身を読み出す方法

`docker-compose.yml` では PostgreSQL のデータボリューム名を `CasruBudgetDB` として定義しています。ボリュームの中身を確認したり、データベースのダンプを取得する代表的な手順を示します（Ubuntu 上での実行を想定）。

- ボリュームの情報を確認する

```bash
docker volume inspect CasruBudgetDB
```

- ボリューム内のファイル一覧を取得する（読み取り専用で Alpine コンテナを使う）

```bash
docker run --rm -v CasruBudgetDB:/data alpine ls -la /data
```

- PostgreSQL コンテナに接続して psql を使う

```bash
docker compose exec -it postgres psql -U "${POSTGRES_USER}" -d "${POSTGRES_DB}"
```

- データベース全体をホストへダンプする（`pg_dump` / `pg_restore` 向けの custom format）

```bash
mkdir -p ./readonly
docker compose --profile tools run --rm postgres-tools sh -c 'pg_dump -Fc -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > ./readonly/budget.dump
```

- 補足: `postgres-tools` はバックアップ専用の補助コンテナです。`postgres` 本体のイメージは `postgres:15` のまま維持しています。

- custom format のダンプをコンテナへリストアする

```bash
docker compose exec -T postgres sh -c 'cat > /tmp/budget.dump && pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" /tmp/budget.dump' < ./readonly/budget.dump
```

注意:

- 本番環境でのダンプ/リストアは運用停止時間や WAL の扱いに注意してください。大きなデータを扱う場合は pg_basebackup やスナップショット方式を検討してください。
- ボリュームの直接操作（ファイルの削除や改変）はデータベースの整合性を損なうため避けてください。ファイル系の操作は必ず DB を停止してから行うか、読み取り専用で行ってください。

---
