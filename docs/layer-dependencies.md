# BudgetManagementBotSystem — レイヤー依存関係図

このドキュメントは、現在のコードベースにおける層単位の依存関係を整理したものです。`Layered Architecture` の観点で、どの層がどの層に依存しているかを図と文章で示します。

## 対象レイヤー

- `Domain`: エンティティ、値オブジェクト、列挙型、Repository インターフェース
- `Application`: ユースケース、アプリケーション向けインターフェース
- `Infrastructure`: EF Core の永続化、Repository 実装、Discord 連携、ファイル保存
- `Presentation`: Discord のスラッシュコマンドモジュール
- `Composition Root`: `Program.cs` による DI 設定

## 依存関係の全体像

```mermaid
graph TD
    Presentation[Presentation\nDiscord Modules]
    Application[Application\nUse Cases / Interfaces]
    Domain[Domain\nEntities / Value Objects / Repositories]
    Infrastructure[Infrastructure\nEF Core / Discord / File Storage]
    CompositionRoot[Composition Root\nProgram.cs]

    Presentation -->|通常は| Application
    Application -->|参照| Domain
    Infrastructure -->|実装 / 参照| Application
    Infrastructure -->|参照| Domain
    CompositionRoot -->|DI 登録| Presentation
    CompositionRoot -->|DI 登録| Application
    CompositionRoot -->|DI 登録| Infrastructure
```

## 層ごとの依存関係

### Domain

Domain は最下層として、他の層に依存しません。`Group`、`User`、`BudgetRequest`、`Money`、`FiscalYear` などの業務ルールの中心を持ちます。

### Application

Application は Domain の型と Repository インターフェースを利用してユースケースを実装します。たとえば、申請登録や承認、却下、予算上限の更新は Application のユースケースに集約されています。

### Infrastructure

Infrastructure は Application のインターフェースを実装し、Domain のエンティティを永続化します。`BudgetManagementDbContext`、`EfUnitOfWork`、Repository 実装、`LocalFileStorage`、`DiscordBotService` がこの層にあります。

### Presentation

Presentation は Discord のコマンド入口です。現状は多くの処理が Application の UseCase / Query UseCase 経由に寄せられていますが、一部のモジュールでは認可判定や一覧表示のために Repository を直接参照しています。

### Composition Root

`Program.cs` は各層の依存を組み立てるだけの場所です。実処理は持たず、DI 登録を通じて層の接続を担当します。

## 現状の例外

現時点では、主に管理系・班系モジュールが Presentation 層としてはやや強い結合を持っています。

- `UserManagementModule` が `IUserRepository` を直接利用して呼び出し元ユーザーを確認している
- `GroupModule` が `IGroupRepository` / `IUserRepository` を直接利用して班一覧や呼び出し元ユーザーを確認している
- `ApprovalModule` は申請一覧・却下・承認取消を `GetPendingRequestsUseCase`、`RequestQueryUseCase`、`RejectBudgetRequestUseCase`、`RevokeApprovalUseCase` などへ寄せている

このため、現状の実装は「完全に Application 経由」ではなく、Presentation が Domain の Repository インターフェースに少し踏み込んでいます。

## 目指す形

将来的には、Presentation が Application のユースケースだけを呼び出し、Repository の直接参照をなくすと、依存関係はより明確になります。特にユーザー管理・班管理の認可判定を Application 側の Command / Query UseCase に寄せると、層の責務分離が分かりやすくなります。

## 参照箇所

- [Program.cs](../src/BudgetManagementBotSystem/Program.cs)
- [ApprovalModule.cs](../src/BudgetManagementBotSystem/Presentation/Discord/Modules/ApprovalModule.cs)
- [BudgetManagementDbContext.cs](../src/BudgetManagementBotSystem/Infrastructure/Persistence/BudgetManagementDbContext.cs)
- [EfUnitOfWork.cs](../src/BudgetManagementBotSystem/Infrastructure/Persistence/EfUnitOfWork.cs)
