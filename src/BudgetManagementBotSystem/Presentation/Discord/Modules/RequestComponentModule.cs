using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using BudgetManagementBotSystem.Presentation.Discord.Services;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules;

public class RequestComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly SubmitBudgetRequestUseCase _submitBudgetRequestUseCase;
    private readonly IUserRepository _userRepository;
    private readonly PendingRequestConfirmationStore _store;
    private readonly DiscordRequestNotificationService _notificationService;
    private readonly ILogger<RequestComponentModule> _logger;

    public RequestComponentModule(
        SubmitBudgetRequestUseCase submitBudgetRequestUseCase,
        IUserRepository userRepository,
        PendingRequestConfirmationStore store,
        DiscordRequestNotificationService notificationService,
        ILogger<RequestComponentModule> logger)
    {
        _submitBudgetRequestUseCase = submitBudgetRequestUseCase;
        _userRepository = userRepository;
        _store = store;
        _notificationService = notificationService;
        _logger = logger;
    }

    [ComponentInteraction("request:create:confirm:*")]
    public async Task ConfirmCreateRequest(string token)
    {
        await DeferAsync(ephemeral: true);

        if (!_store.TryRemove(token, out var confirmation) || confirmation == null)
        {
            await FollowupAsync(
                embed: DiscordEmbedFactory.BuildWarningEmbed(
                    "確認期限が切れています",
                    "もう一度 /create-request から申請を作成してください。"),
                ephemeral: true);
            return;
        }

        if (confirmation.RequesterDiscordUserId != Context.User.Id)
        {
            await FollowupAsync(
                embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この申請確認を操作できるのは申請者本人のみです。"),
                ephemeral: true);
            return;
        }

        try
        {
            var requester = await _userRepository.GetByIdAsync(confirmation.UserId);
            if (requester == null)
            {
                await FollowupAsync(
                    embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("申請者の登録情報を確認できませんでした。"),
                    ephemeral: true);
                return;
            }

            var (requestId, savedEvidenceCount) = await _submitBudgetRequestUseCase.ExecuteAsync(
                confirmation.UserId,
                confirmation.GroupId,
                confirmation.Amount,
                confirmation.Description,
                confirmation.EvidenceFiles);

            var notifiedCount = await _notificationService.NotifyAccountantsAsync(
                requestId,
                confirmation.GroupId,
                confirmation.Amount,
                confirmation.Description,
                requester.Name,
                requester.DiscordUserId);

            await FollowupAsync(
                embed: DiscordEmbedFactory.BuildRequestCreatedEmbed(
                    requestId,
                    confirmation.GroupId,
                    confirmation.Amount,
                    confirmation.Description,
                    savedEvidenceCount,
                    notifiedCount),
                ephemeral: true);
        }
        catch (ArgumentNullException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("入力内容を確認してください。"), ephemeral: true);
        }
        catch (ArgumentOutOfRangeException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("金額を確認してください。"), ephemeral: true);
        }
        catch (BudgetLimitExceededException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildWarningEmbed("予算上限を超過しています", "現在の予算上限を超えるため、申請は作成されませんでした。"), ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to confirm request creation. DiscordUserId: {DiscordUserId}, GroupId: {GroupId}, Amount: {Amount}, EvidenceFileCount: {EvidenceFileCount}",
                Context.User.Id,
                confirmation.GroupId,
                confirmation.Amount,
                confirmation.EvidenceFiles.Count);

            await FollowupAsync(
                embed: DiscordEmbedFactory.BuildErrorEmbed("申請を作成できません", "時間を置いて再実行してください。解決しない場合は管理者に連絡してください。"),
                ephemeral: true);
        }
    }

    [ComponentInteraction("request:create:cancel:*")]
    public async Task CancelCreateRequest(string token)
    {
        if (!_store.TryGet(token, out var confirmation) || confirmation == null)
        {
            await RespondAsync(
                embed: DiscordEmbedFactory.BuildWarningEmbed(
                    "確認期限が切れています",
                    "もう一度 /create-request から申請を作成してください。"),
                ephemeral: true);
            return;
        }

        if (confirmation.RequesterDiscordUserId != Context.User.Id)
        {
            await RespondAsync(
                embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この申請確認を操作できるのは申請者本人のみです。"),
                ephemeral: true);
            return;
        }

        _store.TryRemove(token, out _);

        await RespondAsync(
            embed: DiscordEmbedFactory.BuildInfoEmbed(
                "申請作成をキャンセルしました",
                "申請は作成されていません。"),
            ephemeral: true);
    }
}
