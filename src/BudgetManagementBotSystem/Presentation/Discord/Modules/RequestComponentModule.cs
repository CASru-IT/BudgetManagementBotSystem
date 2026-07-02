using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using BudgetManagementBotSystem.Presentation.Discord.Models;
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
    private readonly RequestWorkflowInteractionService _requestWorkflowInteractionService;
    private readonly ILogger<RequestComponentModule> _logger;

    public RequestComponentModule(
        SubmitBudgetRequestUseCase submitBudgetRequestUseCase,
        IUserRepository userRepository,
        PendingRequestConfirmationStore store,
        DiscordRequestNotificationService notificationService,
        RequestWorkflowInteractionService requestWorkflowInteractionService,
        ILogger<RequestComponentModule> logger)
    {
        _submitBudgetRequestUseCase = submitBudgetRequestUseCase;
        _userRepository = userRepository;
        _store = store;
        _notificationService = notificationService;
        _requestWorkflowInteractionService = requestWorkflowInteractionService;
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

    [ComponentInteraction("request:approve:*")]
    public async Task ApproveRequestFromButton(string requestId)
    {
        await DeferAsync(ephemeral: true);

        if (!int.TryParse(requestId, out var parsedRequestId))
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請IDは数値で指定してください。"), ephemeral: true);
            return;
        }

        try
        {
            var notificationSent = await _requestWorkflowInteractionService.ApproveAsync(parsedRequestId, Context.User.Id);
            await FollowupAsync(embed: DiscordEmbedFactory.BuildApprovalResultEmbed(parsedRequestId, notificationSent), ephemeral: true);
        }
        catch (UnauthorizedAccessException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この申請を承認する権限がありません。"), ephemeral: true);
        }
        catch (ArgumentNullException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", parsedRequestId.ToString()), ephemeral: true);
        }
        catch (ArgumentException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", parsedRequestId.ToString()), ephemeral: true);
        }
        catch (InvalidOperationException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請状態を確認してください。承認できるのは承認待ちの申請です。"), ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve request from button. DiscordUserId: {DiscordUserId}, RequestId: {RequestId}", Context.User.Id, parsedRequestId);
            await FollowupAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("承認処理を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
        }
    }

    [ComponentInteraction("request:reject:*")]
    public async Task ShowRejectModal(string requestId)
    {
        if (!int.TryParse(requestId, out _))
        {
            await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請IDは数値で指定してください。"), ephemeral: true);
            return;
        }

        await RespondWithModalAsync<RejectReasonModal>($"request:reject-modal:{requestId}");
    }

    [ModalInteraction("request:reject-modal:*")]
    public async Task RejectRequestFromModal(string requestId, RejectReasonModal modal)
    {
        await DeferAsync(ephemeral: true);

        if (!int.TryParse(requestId, out var parsedRequestId))
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請IDは数値で指定してください。"), ephemeral: true);
            return;
        }

        var reason = modal.Reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("却下理由を入力してください。"), ephemeral: true);
            return;
        }

        try
        {
            var notificationSent = await _requestWorkflowInteractionService.RejectAsync(parsedRequestId, Context.User.Id, reason);
            await FollowupAsync(embed: DiscordEmbedFactory.BuildRejectionResultEmbed(parsedRequestId, notificationSent, reason), ephemeral: true);
        }
        catch (UnauthorizedAccessException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この申請を却下する権限がありません。"), ephemeral: true);
        }
        catch (ArgumentNullException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", parsedRequestId.ToString()), ephemeral: true);
        }
        catch (ArgumentException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", parsedRequestId.ToString()), ephemeral: true);
        }
        catch (InvalidOperationException)
        {
            await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請状態を確認してください。却下できるのは承認待ちの申請です。"), ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject request from modal. DiscordUserId: {DiscordUserId}, RequestId: {RequestId}", Context.User.Id, parsedRequestId);
            await FollowupAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("却下処理を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
        }
    }
}
