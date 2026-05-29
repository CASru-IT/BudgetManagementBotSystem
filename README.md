# BudgetManagementBotSystem

軽量な説明: Discord 上で動作する予算管理 Bot システムです。

開発・ローカル実行手順は [docs/development.md](docs/development.md) を参照してください。実装・設計の詳細は `docs/` 配下の各資料を参照してください。

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

---

本 README にはデプロイ手順のみを残しました。開発とローカル実行については [docs/development.md](docs/development.md) を確認してください。
