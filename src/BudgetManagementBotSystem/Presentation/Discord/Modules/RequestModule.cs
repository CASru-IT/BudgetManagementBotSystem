using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.Presentation.Discord.Autocomplete;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class RequestModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly SubmitBudgetRequestUseCase _submitBudgetRequestUseCase;
        private readonly CancelBudgetRequestUseCase _cancelBudgetRequestUseCase;
        private readonly UserCancelBudgetRequestUseCase _userCancelRequestUseCase;
        private readonly IUserRepository _userRepository;
        private readonly DiscordBotService _discordBotService;
        private readonly RequestListUseCase _requestListUseCase;
        private readonly RequestDetailUseCase _requestDetailUseCase;
        private readonly ILogger<RequestModule> _logger;

        public RequestModule(
            SubmitBudgetRequestUseCase submitBudgetRequestUseCase,
            CancelBudgetRequestUseCase cancelBudgetRequestUseCase,
            UserCancelBudgetRequestUseCase userCancelRequestUseCase,
            IUserRepository userRepository,
            DiscordBotService discordBotService,
            RequestListUseCase requestListUseCase,
            RequestDetailUseCase requestDetailUseCase,
            ILogger<RequestModule> logger)
        {
            _submitBudgetRequestUseCase = submitBudgetRequestUseCase;
            _cancelBudgetRequestUseCase = cancelBudgetRequestUseCase;
            _userCancelRequestUseCase = userCancelRequestUseCase;
            _userRepository = userRepository;
            _discordBotService = discordBotService;
            _requestListUseCase = requestListUseCase;
            _requestDetailUseCase = requestDetailUseCase;
            _logger = logger;
        }

        [SlashCommand("create-request", "予算使用申請を作成します")]
        public async Task CreateRequest(
            [Summary("group-id"), Autocomplete(typeof(GroupAutocompleteHandler))] string groupId,
            [Summary("amount")] double amount,
            [Summary("description")] string description,
            [Summary("attach-count")] int attachCount = 1)
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

                if (!int.TryParse(groupId, out var parsedGroupId))
                {
                    await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("班IDは数値で指定してください。"), ephemeral: true);
                    return;
                }

                if (attachCount < 1)
                {
                    await FollowupAsync(embed: DiscordEmbedFactory.BuildValidationErrorEmbed("添付ファイル数は1件以上で指定してください。"), ephemeral: true);
                    return;
                }

                var amountDecimal = Convert.ToDecimal(amount);
                await FollowupAsync(embed: DiscordEmbedFactory.BuildInfoEmbed("証跡ファイルをアップロードしてください", $"{attachCount}件の証跡ファイルをこのチャンネルに90秒以内にアップロードしてください。"), ephemeral: true);

                var uploaded = await _discordBotService.WaitForAttachmentUploadAsync(Context.User.Id, TimeSpan.FromSeconds(90), attachCount, Context.Channel);
                if (uploaded == null || !uploaded.Any() || uploaded.Count < attachCount)
                {
                    await FollowupAsync(embed: DiscordEmbedFactory.BuildWarningEmbed("申請を中止しました", "証跡ファイルの受け取りに失敗しました。もう一度申請してください。"), ephemeral: true);
                    return;
                }

                await FollowupAsync(embed: DiscordEmbedFactory.BuildInfoEmbed("証跡ファイルを受け取りました", "保存処理を開始します。"), ephemeral: true);

                var evidenceFiles = new List<UploadedEvidenceDto>(uploaded);
                var (requestId, savedEvidenceCount) = await _submitBudgetRequestUseCase.ExecuteAsync(user.Id, parsedGroupId, amountDecimal, description, evidenceFiles);
                var notifiedCount = await NotifyAccountantsAsync(requestId, parsedGroupId, amountDecimal, description, user.Name, user.DiscordUserId);

                await FollowupAsync(
                    embed: DiscordEmbedFactory.BuildRequestCreatedEmbed(requestId, parsedGroupId, amountDecimal, description, savedEvidenceCount, notifiedCount),
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
                _logger.LogError(ex, "Failed to create request. DiscordUserId: {DiscordUserId}, GroupId: {GroupId}, Amount: {Amount}, AttachCount: {AttachCount}", Context.User.Id, groupId, amount, attachCount);
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
                await RespondAsync(embed: DiscordEmbedFactory.BuildRequestListEmbed(result, status), ephemeral: result.Total == 0);
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
                if (!detail.Evidences.Any())
                {
                    await RespondAsync(embed: embed);
                    return;
                }

                var files = detail.Evidences
                    .Select(evidence => new FileAttachment(new MemoryStream(evidence.Content, writable: false), evidence.FileName))
                    .ToList();

                await RespondWithFilesAsync(files, embeds: new[] { embed });
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
    }
}
