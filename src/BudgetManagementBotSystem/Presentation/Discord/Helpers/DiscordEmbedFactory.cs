using System;
using System.Collections.Generic;
using System.Linq;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using Discord;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers
{
    public static class DiscordEmbedFactory
    {
        private const int MaxListItems = 20;
        private const int ShortTextLength = 80;
        private const int FieldTextLength = 900;

        public static Embed BuildSuccessEmbed(string title, string message)
        {
            return BuildBasicEmbed(title, message, Color.Green);
        }

        public static Embed BuildInfoEmbed(string title, string message)
        {
            return BuildBasicEmbed(title, message, Color.Blue);
        }

        public static Embed BuildWarningEmbed(string title, string message)
        {
            return BuildBasicEmbed(title, message, Color.Gold);
        }

        public static Embed BuildErrorEmbed(string title, string message)
        {
            return BuildBasicEmbed(title, message, Color.Red);
        }

        public static Embed BuildAuthorizationErrorEmbed(string message)
        {
            return BuildErrorEmbed("権限がありません", message);
        }

        public static Embed BuildValidationErrorEmbed(string message)
        {
            return BuildWarningEmbed("入力内容を確認してください", message);
        }

        public static Embed BuildNotFoundEmbed(string targetName, string id)
        {
            return BuildWarningEmbed($"{targetName}が見つかりません", $"{targetName} ID `{id}` を確認してください。");
        }

        public static Embed BuildRemainingBudgetEmbed(RemainingBudgetDto dto)
        {
            var color = dto.AvailableAfterPending < 0 ? Color.Red : Color.Green;

            return new EmbedBuilder()
                .WithTitle($"予算状況 - {dto.GroupName}")
                .WithColor(color)
                .AddField("班", $"{dto.GroupName} (`{dto.GroupId}`)", true)
                .AddField("実残高", dto.ActualBalance.ToString("C"), true)
                .AddField("未承認申請合計", dto.PendingTotal.ToString("C"), true)
                .AddField("申請考慮後残高", dto.AvailableAfterPending.ToString("C"), true)
                .AddField("会計年度", dto.FiscalYear.ToString(), true)
                .WithFooter("未承認申請を差し引いた残高もあわせて確認してください。")
                .Build();
        }

        public static Embed BuildUsageHistoryEmbed(PagedResult<TransactionDto> result, int? requestedFiscalYear = null)
        {
            var totalPages = GetTotalPages(result.Total, result.PageSize);
            var fiscalYear = requestedFiscalYear ?? result.ResolvedFiscalYear;
            var embed = new EmbedBuilder()
                .WithTitle("予算使用履歴")
                .WithColor(Color.Blue)
                .AddField("ページ", $"{result.Page}/{totalPages}", true)
                .AddField("合計件数", result.Total.ToString(), true)
                .AddField("会計年度", fiscalYear.ToString(), true);

            if (result.Total == 0 || !result.Items.Any())
            {
                return embed
                    .WithDescription("条件に一致する取引履歴はありません。")
                    .Build();
            }

            AddTransactionFields(embed, result.Items);
            AddLimitedFooter(embed, result.Items.Count, result.Total);
            return embed.Build();
        }

        public static Embed BuildAllHistoryEmbed(IEnumerable<TransactionDto> transactions, int requestedTake)
        {
            var items = transactions.Take(MaxListItems).ToList();
            var embed = new EmbedBuilder()
                .WithTitle("全班の予算履歴")
                .WithColor(Color.Blue)
                .AddField("表示件数", items.Count.ToString(), true)
                .AddField("取得上限", requestedTake.ToString(), true);

            if (items.Count == 0)
            {
                return embed
                    .WithDescription("取引履歴はありません。")
                    .Build();
            }

            AddTransactionFields(embed, items);
            if (requestedTake > MaxListItems)
            {
                embed.WithFooter($"Discord表示制限により先頭{MaxListItems}件を表示しています。");
            }

            return embed.Build();
        }

        public static Embed BuildBudgetAddedEmbed(string groupName, int groupId, decimal amount, decimal actualBalance, int fiscalYear)
        {
            return new EmbedBuilder()
                .WithTitle("予算を追加しました")
                .WithColor(Color.Green)
                .AddField("班", $"{groupName} (`{groupId}`)", true)
                .AddField("追加金額", amount.ToString("C"), true)
                .AddField("追加後の実残高", actualBalance.ToString("C"), true)
                .AddField("会計年度", fiscalYear.ToString(), true)
                .Build();
        }

        public static Embed BuildTransactionAddedEmbed(int groupId, string groupName, bool isIncome, decimal amount, int fiscalYear, decimal actualBalance)
        {
            var label = isIncome ? "収入" : "支出";

            return new EmbedBuilder()
                .WithTitle("予算取引を追加しました")
                .WithColor(Color.Green)
                .AddField("班", $"{groupName} (`{groupId}`)", true)
                .AddField("種別", label, true)
                .AddField("金額", amount.ToString("C"), true)
                .AddField("会計年度", fiscalYear.ToString(), true)
                .AddField("追加後の実残高", actualBalance.ToString("C"), true)
                .Build();
        }

        public static Embed BuildRequestListEmbed(PagedResult<PendingRequestDto> result, string? status)
        {
            var totalPages = GetTotalPages(result.Total, result.PageSize);
            var embed = new EmbedBuilder()
                .WithTitle("申請一覧")
                .WithColor(Color.Blue)
                .AddField("ページ", $"{result.Page}/{totalPages}", true)
                .AddField("合計件数", result.Total.ToString(), true)
                .AddField("ステータス条件", string.IsNullOrWhiteSpace(status) ? "指定なし" : status, true);

            if (result.Total == 0 || !result.Items.Any())
            {
                return embed
                    .WithDescription("条件に一致する申請はありません。")
                    .Build();
            }

            foreach (var request in result.Items.Take(MaxListItems))
            {
                embed.AddField(
                    $"#{request.Id} / {request.GroupName} / {request.Amount:C}",
                    $"日付: `{request.RequestDate:yyyy-MM-dd}`\nステータス: `{request.Status}`\n説明: {Truncate(request.Description, ShortTextLength)}");
            }

            AddLimitedFooter(embed, result.Items.Count, result.Total);
            return embed.Build();
        }

        public static Embed BuildRequestDetailEmbed(RequestDetailDto detail)
        {
            var request = detail.Request;
            if (request == null)
            {
                return BuildNotFoundEmbed("申請", "unknown");
            }

            var currentStatus = request.GetCurrentStatus();
            var groupLabel = detail.GroupName ?? (detail.GroupId.HasValue ? detail.GroupId.Value.ToString() : "不明");
            var requesterName = string.IsNullOrWhiteSpace(detail.RequesterName) ? "不明" : detail.RequesterName;
            var requesterDiscordId = detail.RequesterDiscordUserId?.ToString() ?? "不明";
            var evidenceText = detail.Evidences.Any()
                ? string.Join("\n", detail.Evidences.Select(e => Truncate(e.FileName, ShortTextLength)))
                : "証跡ファイルはありません。";
            var historyText = string.Join("\n", request.GetOrderedStatusHistory().Select(s => $"{s.ChangedStatus} @ {s.ChangedAt:yyyy-MM-dd HH:mm}"));
            if (string.IsNullOrWhiteSpace(historyText))
            {
                historyText = "履歴はありません。";
            }

            var embed = new EmbedBuilder()
                .WithTitle($"申請詳細 #{request.Id}")
                .WithColor(GetStatusColor(currentStatus))
                .AddField("班", groupLabel, true)
                .AddField("金額", request.Amount.Value.ToString("C"), true)
                .AddField("ステータス", currentStatus.ToString(), true)
                .AddField("申請日", request.RequestDate.ToString("yyyy-MM-dd HH:mm"), true)
                .AddField("申請者", requesterName, true)
                .AddField("申請者Discord ID", requesterDiscordId, true)
                .AddField("説明", Truncate(request.Description, FieldTextLength))
                .AddField("証跡", Truncate(evidenceText, FieldTextLength))
                .AddField("履歴", Truncate(historyText, FieldTextLength))
                .WithFooter(GetRequestDetailFooter(currentStatus, request.Id));

            if (detail.MissingEvidencePaths.Any())
            {
                embed.AddField("添付できない証跡", Truncate(string.Join("\n", detail.MissingEvidencePaths), FieldTextLength));
            }

            return embed.Build();
        }

        public static Embed BuildRequestCreatedEmbed(int requestId, int groupId, decimal amount, string description, int savedEvidenceCount, int notifiedCount)
        {
            return new EmbedBuilder()
                .WithTitle("申請を作成しました")
                .WithColor(Color.Green)
                .AddField("申請ID", $"#{requestId}", true)
                .AddField("班ID", groupId.ToString(), true)
                .AddField("金額", amount.ToString("C"), true)
                .AddField("証跡", $"{savedEvidenceCount}件保存", true)
                .AddField("通知", $"会計担当者 {notifiedCount}名へDM送信", true)
                .AddField("説明", Truncate(description, ShortTextLength))
                .WithFooter($"/request-detail request-id:{requestId} で詳細を確認できます。")
                .Build();
        }

        public static Embed BuildUserListEmbed(IEnumerable<User> users)
        {
            var orderedUsers = users.OrderBy(u => u.Id).ToList();
            var embed = new EmbedBuilder()
                .WithTitle("ユーザー一覧")
                .WithColor(Color.Blue)
                .AddField("登録ユーザー数", orderedUsers.Count.ToString(), true);

            if (orderedUsers.Count == 0)
            {
                return embed
                    .WithDescription("登録ユーザーはいません。")
                    .Build();
            }

            foreach (var user in orderedUsers.Take(MaxListItems))
            {
                embed.AddField(
                    $"ID {user.Id} / {user.Name}",
                    $"Role: `{user.Role}`\n班ID: `{(user.GroupId.HasValue ? user.GroupId.Value.ToString() : "未所属")}`\n状態: {(user.IsActive ? "有効" : "無効")}",
                    true);
            }

            AddLimitedFooter(embed, Math.Min(orderedUsers.Count, MaxListItems), orderedUsers.Count);
            return embed.Build();
        }

        public static Embed BuildUserInfoEmbed(User user)
        {
            return new EmbedBuilder()
                .WithTitle($"ユーザー情報 - {user.Name}")
                .WithColor(user.IsActive ? Color.Blue : Color.DarkGrey)
                .AddField("ユーザーID", user.Id.ToString(), true)
                .AddField("名前", user.Name, true)
                .AddField("DiscordUserId", user.DiscordUserId.ToString(), true)
                .AddField("Role", user.Role.ToString(), true)
                .AddField("所属班", user.GroupId.HasValue ? $"班ID {user.GroupId.Value}" : "未所属", true)
                .AddField("状態", user.IsActive ? "有効" : "無効", true)
                .Build();
        }

        public static Embed BuildGroupMembersEmbed(int groupId, IEnumerable<User> members)
        {
            var orderedMembers = members.OrderBy(u => u.Id).ToList();
            var embed = new EmbedBuilder()
                .WithTitle($"班メンバー一覧 - 班ID {groupId}")
                .WithColor(Color.Blue)
                .AddField("メンバー数", orderedMembers.Count.ToString(), true);

            if (orderedMembers.Count == 0)
            {
                return embed
                    .WithDescription("指定された班のメンバーはいません。")
                    .Build();
            }

            foreach (var member in orderedMembers.Take(MaxListItems))
            {
                embed.AddField(
                    $"ID {member.Id} / {member.Name}",
                    $"Role: `{member.Role}`\n状態: {(member.IsActive ? "有効" : "無効")}",
                    true);
            }

            AddLimitedFooter(embed, Math.Min(orderedMembers.Count, MaxListItems), orderedMembers.Count);
            return embed.Build();
        }

        public static Embed BuildGroupListEmbed(IEnumerable<GroupListItemDto> groups)
        {
            var orderedGroups = groups.OrderBy(g => g.Id).ToList();
            var embed = new EmbedBuilder()
                .WithTitle("班一覧")
                .WithColor(Color.Blue)
                .AddField("登録班数", orderedGroups.Count.ToString(), true);

            if (orderedGroups.Count == 0)
            {
                return embed
                    .WithDescription("登録済みの班はありません。")
                    .Build();
            }

            foreach (var group in orderedGroups.Take(MaxListItems))
            {
                embed.AddField(group.Name, $"班ID: `{group.Id}`", true);
            }

            AddLimitedFooter(embed, Math.Min(orderedGroups.Count, MaxListItems), orderedGroups.Count);
            return embed.Build();
        }

        public static Embed BuildMenuEmbed(User user)
        {
            var embed = new EmbedBuilder()
                .WithTitle("利用可能なコマンド")
                .WithColor(Color.Blue)
                .WithDescription($"{user.Name} さんのRole: `{user.Role}`");

            embed.AddField("一般", "`/create-request`\n`/list-requests`\n`/request-detail`\n`/cancel-request`\n`/remaining-budget`");

            if (user.Role == AccountRole.Accountant || user.Role == AccountRole.President || user.Role == AccountRole.Admin)
            {
                embed.AddField("会計担当者向け", "`/pending-list`\n`/approve`\n`/reject`\n`/usage-history`");
            }

            if (user.Role == AccountRole.Admin || user.Role == AccountRole.President)
            {
                embed.AddField("管理者向け", "`/register-user`\n`/set-user-role`\n`/assign-group`\n`/register-group`\n`/add-budget`\n`/admin-add-transaction`\n`/list-users`\n`/test-dm`");
            }

            return embed
                .WithFooter("各コマンドの引数はDiscordの候補表示に従って入力してください。")
                .Build();
        }

        public static Embed BuildPendingRequestsEmbed(PagedResult<PendingRequestDto> result)
        {
            var totalPages = GetTotalPages(result.Total, result.PageSize);

            var embed = new EmbedBuilder()
                .WithTitle("未承認申請一覧")
                .WithColor(Color.Blue)
                .AddField("ページ", $"{result.Page}/{totalPages}", true)
                .AddField("合計申請数", result.Total.ToString(), true);

            if (!result.Items.Any())
            {
                embed.WithDescription("現在、未承認の申請はありません。");
                return embed.Build();
            }

            foreach (var request in result.Items.Take(MaxListItems))
            {
                embed.AddField(
                    $"#{request.Id} / {request.GroupName} / {request.Amount:C}",
                    $"申請日: `{request.RequestDate:yyyy-MM-dd}`\n説明: {Truncate(request.Description, ShortTextLength)}");
            }

            AddLimitedFooter(embed, result.Items.Count, result.Total, "承認・却下は /approve /reject コマンドを使用してください。");
            return embed.Build();
        }

        public static Embed BuildApprovalResultEmbed(int requestId, bool notificationSent)
        {
            return new EmbedBuilder()
                .WithTitle("申請を承認しました")
                .WithColor(Color.Green)
                .WithDescription($"申請 `#{requestId}` の承認が完了しました。")
                .AddField("申請ID", requestId, true)
                .AddField("DM通知", notificationSent ? "送信済み" : "未送信または失敗", true)
                .WithFooter($"/request-detail request-id:{requestId} で詳細を確認できます。")
                .Build();
        }

        public static Embed BuildNewRequestAccountantDmEmbed(
            int requestId,
            int groupId,
            decimal amount,
            string description,
            string requesterName,
            ulong requesterDiscordUserId)
        {
            return new EmbedBuilder()
                .WithTitle("新しい予算使用申請があります")
                .WithColor(Color.Gold)
                .WithDescription("内容を確認し、必要に応じて承認または却下してください。")
                .AddField("申請ID", requestId, true)
                .AddField("班ID", groupId, true)
                .AddField("金額", amount.ToString("C"), true)
                .AddField("申請者", requesterName, true)
                .AddField("申請者Discord ID", requesterDiscordUserId.ToString(), true)
                .AddField("説明", Truncate(description, FieldTextLength))
                .WithFooter($"/request-detail request-id:{requestId} で詳細を確認できます。")
                .WithCurrentTimestamp()
                .Build();
        }

        public static Embed BuildApprovedRequestDmEmbed(ApprovedRequestNotificationDto notification)
        {
            return new EmbedBuilder()
                .WithTitle("申請が承認されました")
                .WithColor(Color.Green)
                .WithDescription("あなたの申請が承認されました。")
                .AddField("申請ID", notification.RequestId, true)
                .AddField("班名", notification.GroupName, true)
                .AddField("金額", notification.Amount.ToString("C"), true)
                .AddField("説明", Truncate(notification.Description, ShortTextLength))
                .AddField("承認者", notification.ApproverName, true)
                .AddField("承認者Discord ID", notification.ApproverDiscordUserId.ToString(), true)
                .WithFooter("会計担当者と受け取り日時を調整してください。")
                .Build();
        }

        public static Embed BuildRejectionResultEmbed(int requestId, bool notificationSent, string? reason = null)
        {
            var embed = new EmbedBuilder()
                .WithTitle("申請を却下しました")
                .WithColor(Color.Red)
                .WithDescription($"申請 `#{requestId}` の却下が完了しました。")
                .AddField("申請ID", requestId, true)
                .AddField("DM通知", notificationSent ? "送信済み" : "未送信または失敗", true)
                .WithFooter($"/request-detail request-id:{requestId} で詳細を確認できます。");

            if (!string.IsNullOrWhiteSpace(reason))
            {
                embed.AddField("却下理由", Truncate(reason, FieldTextLength));
            }

            return embed.Build();
        }

        public static Embed BuildRejectedRequestDmEmbed(RejectedRequestNotificationDto notification)
        {
            var embed = new EmbedBuilder()
                .WithTitle("申請が却下されました")
                .WithColor(Color.Red)
                .WithDescription("あなたの申請が却下されました。")
                .AddField("申請ID", notification.RequestId, true)
                .AddField("班名", notification.GroupName, true)
                .AddField("金額", notification.Amount.ToString("C"), true)
                .AddField("説明", Truncate(notification.Description, ShortTextLength))
                .AddField("却下者", notification.RejecterName, true)
                .AddField("却下者Discord ID", notification.RejecterDiscordUserId.ToString(), true);

            if (!string.IsNullOrWhiteSpace(notification.Reason))
            {
                embed.AddField("却下理由", Truncate(notification.Reason, FieldTextLength));
            }

            return embed
                .WithFooter("不明点がある場合は却下者に確認してください。")
                .Build();
        }

        private static Embed BuildBasicEmbed(string title, string message, Color color)
        {
            return new EmbedBuilder()
                .WithTitle(title)
                .WithColor(color)
                .WithDescription(Truncate(message, FieldTextLength))
                .Build();
        }

        private static void AddTransactionFields(EmbedBuilder embed, IEnumerable<TransactionDto> transactions)
        {
            foreach (var transaction in transactions.Take(MaxListItems))
            {
                var type = transaction.IsIncome ? "収入" : "支出";
                embed.AddField(
                    $"{transaction.GroupName} / {type} / {transaction.Amount:C}",
                    $"日付: `{transaction.TransactionDate:yyyy-MM-dd}`\n会計年度: `{transaction.FiscalYear}`");
            }
        }

        private static void AddLimitedFooter(EmbedBuilder embed, int displayedCount, int total, string? suffix = null)
        {
            var footer = total > displayedCount
                ? $"表示件数が多いため一部のみ表示しています。表示: {displayedCount}/{total}"
                : $"表示: {displayedCount}/{total}";

            if (!string.IsNullOrWhiteSpace(suffix))
            {
                footer = $"{footer} / {suffix}";
            }

            embed.WithFooter(footer);
        }

        private static int GetTotalPages(int total, int pageSize)
        {
            return pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        }

        private static Color GetStatusColor(RequestStatus status)
        {
            return status switch
            {
                RequestStatus.Pending => Color.Gold,
                RequestStatus.Approved => Color.Green,
                RequestStatus.Rejected => Color.Red,
                RequestStatus.Cancelled => Color.DarkGrey,
                RequestStatus.ApprovalCancelled => Color.Orange,
                _ => Color.Blue
            };
        }

        private static string GetRequestDetailFooter(RequestStatus status, int requestId)
        {
            return status switch
            {
                RequestStatus.Pending => $"/approve request-id:{requestId} または /reject request-id:{requestId} で処理できます。",
                RequestStatus.Approved => $"/revoke-approval request-id:{requestId} で承認取消できます。",
                _ => "必要に応じて申請者または管理者に確認してください。"
            };
        }

        private static string Truncate(string? text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "なし";
            }

            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }
    }
}
