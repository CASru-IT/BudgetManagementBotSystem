# コードリファクタ要約

このドキュメントは、最近行ったリファクタと主要なコード変更の要約を示します。目的はドキュメントとコードベースの整合性を確保することです。

## 目的

- Presentation 層が直接 DbContext を参照しないようにする。
- Application 層に UseCase を集約することで責務を明確化する。
- ドメインエンティティの非 null 警告を解消する。

## 主要な変更点（ファイル）

- Application/UseCases 配下にドメイン別サブフォルダを追加および移行:
  - `Application/UseCases/UserManagement/RegisterUserUseCase.cs` (移動/再実装)
  - `Application/UseCases/Groups/RegisterGroupUseCase.cs`
  - `Application/UseCases/Groups/DeleteGroupUseCase.cs` (新規)
  - `Application/UseCases/Budget/IncreaseBudgetLimitUseCase.cs` (新規)

- Presentation 層モジュールの using/DI を更新:
  - `Presentation/Discord/Modules/UserManagementModule.cs`
  - `Presentation/Discord/Modules/GroupModule.cs`
  - `Presentation/Discord/Modules/SystemModule.cs`

- ドメインエンティティの非 null 警告対応（初期化子を追加）:
  - `Domain/Entities/BudgetRequest.cs`
  - `Domain/Entities/BudgetTransaction.cs`
  - `Domain/Entities/Group.cs`
  - `Domain/Entities/RequestEvidence.cs`
  - `Domain/Entities/User.cs`

## DI / Program.cs の変更

- UseCase の名前空間を追加し、`Program.cs` の `using` と DI 登録を整理しました。

## テストとビルド

- `dotnet build` → 成功
- `dotnet test --no-build` → 成功

## 今後の推奨作業

- `required` を導入してコンストラクタレベルで初期化を強制するリファクタ（互換性確認が必要）。

---

- 更新履歴:

- 2026-05-26: 初回作成（リファクタ適用後の要約）
- 2026-05-26: `UserManagementModule` のスラッシュ引数を文字列の Discord ID から `IUser` (user) へ変更。
  - コマンド引数名: `discord-user-id` -> `user`
  - 理由: Discord のスラッシュコマンド UI でユーザー選択を行い、`targetUser.Id` を利用するため。
