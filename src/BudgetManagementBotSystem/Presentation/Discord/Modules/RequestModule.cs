using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.Presentation.Discord.Autocomplete;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using BudgetManagementBotSystem.Presentation.Discord.Models;
using BudgetManagementBotSystem.Presentation.Discord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class RequestModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CancelBudgetRequestUseCase _cancelBudgetRequestUseCase;
        private readonly UserCancelBudgetRequestUseCase _userCancelRequestUseCase;
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly DiscordBotService _discordBotService;
        private readonly RequestListUseCase _requestListUseCase;
        private readonly RequestDetailUseCase _requestDetailUseCase;
        private readonly BudgetQueryUseCase _budgetQueryUseCase;
        private readonly PendingRequestConfirmationStore _confirmationStore;
        private readonly PagingSessionStore _pagingSessionStore;
        private readonly ILogger<RequestModule> _logger;

        public RequestModule(
            CancelBudgetRequestUseCase cancelBudgetRequestUseCase,
            UserCancelBudgetRequestUseCase userCancelRequestUseCase,
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            DiscordBotService discordBotService,
            RequestListUseCase requestListUseCase,
            RequestDetailUseCase requestDetailUseCase,
            BudgetQueryUseCase budgetQueryUseCase,
            PendingRequestConfirmationStore confirmationStore,
            PagingSessionStore pagingSessionStore,
            ILogger<RequestModule> logger)
        {
            _cancelBudgetRequestUseCase = cancelBudgetRequestUseCase;
            _userCancelRequestUseCase = userCancelRequestUseCase;
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _discordBotService = discordBotService;
            _requestListUseCase = requestListUseCase;
            _requestDetailUseCase = requestDetailUseCase;
            _budgetQueryUseCase = budgetQueryUseCase;
            _confirmationStore = confirmationStore;
            _pagingSessionStore = pagingSessionStore;
            _logger = logger;
        }

        [SlashCommand("create-request", "予算使用申請を作成します")]
        public async Task CreateRequest(
            [Summary("group-id", "申請する班ID"), Autocomplete(typeof(GroupAutocompleteHandler))] int groupId,
            [Summary("amount", "申請金額")] double amount,
            [Summary("description", "購入目的や内容")] string description,
            [Summary("evidence-1", "領収書・請求書などの証憑ファイル")] IAttachment evidence1,
            [Summary("evidence-2", "追加の証憑ファイル")] IAttachment? evidence2 = null,
            [Summary("evidence-3", "追加の証憑ファイル")] IAttachment? evidence3 = null,
            [Summary("evidence-4", "追加の証憑ファイル")] IAttachment? evidence4 = null,
            [Summary("evidence-5", "追加の証憑ファイル")] IAttachment? evidence5 = null)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var user = await _userRepository.GetByDiscordUserIdAsync(Context.User.Id);
                if (user == null)
                {
                    await FollowupAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。管理者に登録を依頼してください。"), ephemeral: true);
                    return;
                }

                if (amount < 0)
                {
                    await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("金額は0以上で指定してください。"), ephemeral: true);
                    return;
                }

                var amountDecimal = Convert.ToDecimal(amount);
                var group = await _groupRepository.GetByIdAsync(groupId);
                if (group == null)
                {
                    await FollowupAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("班", groupId.ToString()), ephemeral: true);
                    return;
                }

                var attachments = new[] { evidence1, evidence2, evidence3, evidence4, evidence5 }
                    .Where(attachment => attachment != null)
                    .Cast<IAttachment>()
                    .ToList();

                var validationErrors = EvidenceAttachmentValidator.Validate(attachments);
                if (validationErrors.Count > 0)
                {
                    await FollowupAsync(
                        embed: DiscordEmbedFactory.BuildValidationErrorEmbed(
                            "証憑ファイルを登録できません。\n\n理由:\n- " + string.Join("\n- ", validationErrors)),
                        ephemeral: true);
                    return;
                }

                var evidenceFiles = new List<UploadedEvidenceDto>();
                foreach (var attachment in attachments)
                {
                    var downloaded = await _discordBotService.DownloadAttachmentAsync(attachment);
                    if (downloaded == null)
                    {
                        await FollowupAsync(
                            embed: DiscordEmbedFactory.BuildWarningEmbed(
                                "証憑ファイルを取得できません",
                                "添付ファイルのダウンロードに失敗しました。もう一度 /create-request から申請してください。"),
                            ephemeral: true);
                        return;
                    }

                    evidenceFiles.Add(downloaded);
                }

                RemainingBudgetDto? remainingBudget = null;
                try
                {
                    remainingBudget = await _budgetQueryUseCase.GetRemainingBudgetAsync(Context.User.Id, groupId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load remaining budget for request confirmation. DiscordUserId: {DiscordUserId}, GroupId: {GroupId}, Amount: {Amount}, EvidenceFileCount: {EvidenceFileCount}", Context.User.Id, groupId, amount, evidenceFiles.Count);
                }

                var token = _confirmationStore.Create(new PendingRequestConfirmation
                {
                    RequesterDiscordUserId = Context.User.Id,
                    UserId = user.Id,
                    GroupId = groupId,
                    GroupName = group.Name,
                    Amount = amountDecimal,
                    Description = description,
                    EvidenceFiles = evidenceFiles
                });

                var components = new ComponentBuilder()
                    .WithButton("申請する", $"request:create:confirm:{token}", ButtonStyle.Success)
                    .WithButton("キャンセル", $"request:create:cancel:{token}", ButtonStyle.Secondary)
                    .Build();

                await FollowupAsync(
                    embed: DiscordEmbedFactory.BuildRequestConfirmationEmbed(group.Name, groupId, amountDecimal, description, evidenceFiles, remainingBudget),
                    components: components,
                    ephemeral: true);
            }
            catch (ArgumentNullException)
            {
                await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("入力内容を確認してください。"), ephemeral: true);
            }
            catch (ArgumentOutOfRangeException)
            {
                await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("金額や添付ファイル数を確認してください。"), ephemeral: true);
            }
            catch (BudgetLimitExceededException)
            {
                await FollowupAsync(embed: DiscordEmbedFactory.BuildWarningEmbed("予算上限を超過しています", "現在の予算上限を超えるため、申請は作成されませんでした。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                var evidenceFileCount = new[] { evidence1, evidence2, evidence3, evidence4, evidence5 }.Count(attachment => attachment != null);
                _logger.LogError(ex, "Failed to prepare request confirmation. DiscordUserId: {DiscordUserId}, GroupId: {GroupId}, Amount: {Amount}, EvidenceFileCount: {EvidenceFileCount}", Context.User.Id, groupId, amount, evidenceFileCount);
                await FollowupAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("申請を作成できません", "時間を置いて再実行してください。解決しない場合は管理者に連絡してください。"), ephemeral: true);
            }
        }

        [SlashCommand("list-requests", "自分の班または権限内の申請一覧を表示します")]
        public async Task ListRequests(
            [Summary("status")] string? status = null,
            [Summary("page")] int page = 1,
            [Summary("page-size")] int pageSize = 10,
            [Summary("group-id"), Autocomplete(typeof(GroupAutocompleteHandler))] int? groupId = null)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Min(Math.Max(1, pageSize), 50);

                var result = await _requestListUseCase.ExecuteAsync(Context.User.Id, status, page, pageSize, groupId);
                var totalPages = CalculateTotalPages(result.Total, result.PageSize);
                var components = totalPages > 1
                    ? DiscordComponentFactory.BuildPagingComponents(
                        _pagingSessionStore.Create(new PagingSession
                        {
                            OwnerDiscordUserId = Context.User.Id,
                            Target = PagingTarget.RequestList,
                            Page = result.Page,
                            PageSize = result.PageSize,
                            Status = status,
                            GroupId = groupId
                        }),
                        result.Page,
                        totalPages)
                    : null;

                await RespondAsync(
                    embed: DiscordEmbedFactory.BuildRequestListEmbed(result, status),
                    components: components,
                    ephemeral: result.Total == 0);
            }
            catch (ArgumentException)
            {
                await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。管理者に登録を依頼してください。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list requests. DiscordUserId: {DiscordUserId}, Status: {Status}, Page: {Page}, PageSize: {PageSize}, GroupId: {GroupId}", Context.User.Id, status, page, pageSize, groupId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("申請一覧を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("request-detail", "指定した申請の詳細を表示します")]
        public async Task RequestDetail([Summary("request-id"), Autocomplete(typeof(RequestAutocompleteHandler))] string requestId)
        {
            try
            {
                if (!int.TryParse(requestId, out var parsedRequestId))
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請IDは数値で指定してください。"), ephemeral: true);
                    return;
                }

                var detail = await _requestDetailUseCase.GetByIdAsync(parsedRequestId);
                if (detail.Request == null)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", parsedRequestId.ToString()), ephemeral: true);
                    return;
                }

                var embed = DiscordEmbedFactory.BuildRequestDetailEmbed(detail);
                var currentStatus = detail.Request.GetCurrentStatus();
                var components = DiscordComponentFactory.BuildRequestDetailComponents(parsedRequestId, currentStatus);
                if (!detail.Evidences.Any())
                {
                    await RespondAsync(embed: embed, components: components);
                    return;
                }

                var files = detail.Evidences
                    .Select(evidence => new FileAttachment(new MemoryStream(evidence.Content, writable: false), evidence.FileName))
                    .ToList();

                await RespondWithFilesAsync(files, embeds: new[] { embed }, components: components);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get request detail. DiscordUserId: {DiscordUserId}, RequestId: {RequestId}", Context.User.Id, requestId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("申請詳細を取得できません", "時間を置いて再実行してください。"), ephemeral: true);
            }
        }

        [SlashCommand("cancel-request", "承認待ち状態の申請を取り消します")]
        public async Task CancelRequest([Summary("request-id"), Autocomplete(typeof(RequestAutocompleteHandler))] string requestId)
        {
            try
            {
                if (!int.TryParse(requestId, out var parsedRequestId))
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("申請IDは数値で指定してください。"), ephemeral: true);
                    return;
                }

                var user = await _userRepository.GetByDiscordUserIdAsync(Context.User.Id);
                if (user == null)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。"), ephemeral: true);
                    return;
                }

                var detail = await _requestDetailUseCase.GetByIdAsync(parsedRequestId);
                var request = detail.Request;
                if (request == null || detail.GroupId == null)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildNotFoundEmbed("申請", parsedRequestId.ToString()), ephemeral: true);
                    return;
                }

                var isRequestOwner = request.UserId == user.Id;
                if (isRequestOwner && request.GetCurrentStatus() == RequestStatus.Pending)
                {
                    await _userCancelRequestUseCase.ExecuteAsync(detail.GroupId.Value, parsedRequestId, user.Id);
                    await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("申請を取り消しました", $"申請 `#{parsedRequestId}` を申請者として取り消しました。"), ephemeral: true);
                    return;
                }

                var isPrivilegedCancel = await AuthorizationHelper.IsPrivilegedAsync(_userRepository, Context.User.Id, AccountRole.Admin);
                if (!isPrivilegedCancel)
                {
                    await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この申請を取り消す権限がありません。"), ephemeral: true);
                    return;
                }

                await _cancelBudgetRequestUseCase.ExecuteAsync(detail.GroupId.Value, parsedRequestId, user.Id);
                await RespondAsync(embed: DiscordEmbedFactory.BuildSuccessEmbed("申請を取り消しました", $"申請 `#{parsedRequestId}` を管理者として取り消しました。"), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel request. DiscordUserId: {DiscordUserId}, RequestId: {RequestId}", Context.User.Id, requestId);
                await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("申請を取り消せません", "申請状態や権限を確認してから再実行してください。"), ephemeral: true);
            }
        }

        private async Task<int> NotifyAccountantsAsync(int requestId, int groupId, decimal amount, string description, string requesterName, ulong requesterDiscordUserId)
        {
            var users = await _userRepository.GetAllAsync();
            if (users == null)
            {
                return 0;
            }

            var accountantUsers = users
                .Where(user => user.IsActive && user.Role == AccountRole.Accountant)
                .ToList();

            if (accountantUsers.Count == 0)
            {
                return 0;
            }

            var embed = DiscordEmbedFactory.BuildNewRequestAccountantDmEmbed(
                requestId,
                groupId,
                amount,
                description,
                requesterName,
                requesterDiscordUserId);

            var sendTasks = accountantUsers.Select(accountant => _discordBotService.SendDirectMessageAsync(accountant.DiscordUserId, embed));
            var results = await Task.WhenAll(sendTasks);
            return results.Count(result => result);
        }

        private static int CalculateTotalPages(int total, int pageSize)
        {
            return pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        }
    }
}
