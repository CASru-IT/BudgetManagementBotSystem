using System.Globalization;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Presentation.Discord.Models;
using Discord;

namespace BudgetManagementBotSystem.Presentation.Discord.Services;

public sealed class BudgetActivityChannel
{
    private readonly IDiscordClient _client;
    private readonly ILogger<BudgetActivityChannel> _logger;
    private readonly ulong? _channelId;

    public BudgetActivityChannel(IDiscordClient client, IConfiguration configuration, ILogger<BudgetActivityChannel> logger)
    {
        _client = client;
        _logger = logger;
        var setting = configuration["Discord:BudgetActivityChannelId"]?.Trim();
        if (string.IsNullOrEmpty(setting)) return;

        if (ulong.TryParse(setting, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
            _channelId = id;
        else
            _logger.LogWarning("Discord:BudgetActivityChannelId must be a positive numeric channel ID. Budget activity notifications are disabled.");
    }

    public Task NotifyRequestCreatedAsync(PendingRequestConfirmation request, int requestId) =>
        SendAsync("予算申請が作成されました", request.RequesterDiscordUserId, request.GroupName,
            request.GroupId, request.Amount, $"申請ID #{requestId}");

    public Task NotifyTransactionAsync(string title, ulong actorId, BudgetTransactionResult result) =>
        SendAsync(title, actorId, result.GroupName, result.GroupId, result.Amount,
            $"{result.FiscalYear}年度・履歴ID #{result.TransactionId}");

    private async Task SendAsync(string title, ulong actorId, string groupName, int groupId, decimal amount, string reference)
    {
        if (_channelId is not ulong channelId) return;

        try
        {
            if (await _client.GetChannelAsync(channelId) is not ITextChannel channel)
            {
                _logger.LogWarning("Cannot post {Activity} ({Reference}): Discord text channel {ChannelId} is unavailable.", title, reference, channelId);
                return;
            }

            // PostgreSQL stores ledger amounts as numeric(18,2), rounding midpoint values away from zero.
            var storedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(Color.Blue)
                .AddField("実行者", MentionUtils.MentionUser(actorId), true)
                .AddField("班", $"{groupName} (`{groupId}`)", true)
                .AddField("金額", storedAmount.ToString("#,0.##", CultureInfo.InvariantCulture) + " 円", true)
                .WithFooter(reference)
                .Build();
            await channel.SendMessageAsync(embed: embed, allowedMentions: AllowedMentions.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not post {Activity} ({Reference}) to Discord channel {ChannelId}.", title, reference, channelId);
        }
    }
}
