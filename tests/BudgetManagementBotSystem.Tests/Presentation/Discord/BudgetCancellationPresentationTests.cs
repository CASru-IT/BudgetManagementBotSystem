using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using BudgetManagementBotSystem.Presentation.Discord.Services;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class BudgetCancellationPresentationTests
{
    private static BudgetCancellationPreview Preview(bool legacy = false) => new(
        123, 8, "ハリボテ班", 5000, 2025, new DateTime(2025, 10, 1, 1, 0, 0, DateTimeKind.Utc),
        35000, 30000, 1000, Guid.NewGuid(), legacy, "班を間違えた");

    [Fact]
    public void ConfirmationCannotBeConsumedByAnotherUserOrTwice()
    {
        var store = new BudgetCancellationConfirmationStore();
        var token = store.Create(100, Preview());
        Assert.False(store.TryTake(token, 999, out _));
        Assert.True(store.TryTake(token, 100, out var confirmation));
        Assert.Equal(123, confirmation!.Preview.TransactionId);
        Assert.Equal("班を間違えた", confirmation.Preview.Reason);
        Assert.False(store.TryTake(token, 100, out _));
    }

    [Fact]
    public async Task ConcurrentClicksOnlyConsumeOnce()
    {
        var store = new BudgetCancellationConfirmationStore();
        var token = store.Create(100, Preview());
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() => store.TryTake(token, 100, out _))));
        Assert.Single(results, success => success);
    }

    [Fact]
    public void ConfirmationExpiresAtTenMinutes_AndRestartLosesTokens()
    {
        var clock = new TestClock();
        var store = new BudgetCancellationConfirmationStore(clock);
        var token = store.Create(100, Preview());
        clock.Now += TimeSpan.FromMinutes(10);
        Assert.False(store.TryTake(token, 100, out _));
        Assert.False(new BudgetCancellationConfirmationStore().TryTake(store.Create(100, Preview()), 100, out _));
    }

    [Fact]
    public void PreviewShowsTargetYearReasonBalancesAndLegacyCaution()
    {
        var embed = BudgetCancellationEmbeds.Confirmation(Preview(true));
        var text = string.Join("\n", embed.Fields.Select(f => f.Name + f.Value)) + embed.Description;
        Assert.Contains("ハリボテ班", text);
        Assert.Contains("123", text);
        Assert.Contains("2025", text);
        Assert.Contains("35,000", text);
        Assert.Contains("30,000", text);
        Assert.Contains("班を間違えた", text);
        Assert.Contains("由来", text);
        Assert.Contains("返金", text);
        Assert.DoesNotContain("由来", BudgetCancellationEmbeds.Confirmation(Preview()).Description ?? "");
    }

    [Fact]
    public void HistoryIdentifiesOriginalCancellationAndActor()
    {
        var result = new PagedResult<TransactionDto>
        {
            Total = 2, Page = 1, PageSize = 10, ResolvedFiscalYear = 2025,
            Items = [
                new() { TransactionId = 123, GroupName = "ハリボテ班", IsIncome = true, Amount = 5000, FiscalYear = 2025, ReversedByTransactionId = 124 },
                new() { TransactionId = 124, GroupName = "ハリボテ班", Amount = 5000, FiscalYear = 2025,
                    Kind = BudgetTransactionKind.BudgetCancellation, ReversesTransactionId = 123, CancelledByUserId = 7, CancellationReason = "班を間違えた" }
            ]
        };
        var embed = DiscordEmbedFactory.BuildUsageHistoryEmbed(result);
        var text = string.Join("\n", embed.Fields.Select(f => f.Name + f.Value));
        Assert.Contains("123", text);
        Assert.Contains("124", text);
        Assert.Contains("取消済", text);
        Assert.Contains("予算取消", text);
        Assert.Contains("班を間違えた", text);
        Assert.Contains("実行者", text);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void HistoryWithLongReasonsStaysWithinDiscordLimits()
    {
        var items = Enumerable.Range(1, 20).Select(id => new TransactionDto
        {
            TransactionId = id, GroupName = new string('班', 200), Amount = 5000, FiscalYear = 2025,
            Kind = BudgetTransactionKind.BudgetCancellation, ReversesTransactionId = id + 100,
            CancellationReason = new string('理', 500), CancelledByUserId = 7
        }).ToList();
        var embed = DiscordEmbedFactory.BuildUsageHistoryEmbed(new PagedResult<TransactionDto>
            { Total = 20, Page = 1, PageSize = 20, Items = items });
        Assert.True(embed.Title.Length + embed.Fields.Sum(f => f.Name.Length + f.Value.Length) + embed.Footer!.Value.Text.Length <= 6000);
        Assert.All(embed.Fields, f => Assert.True(f.Name.Length <= 256));
    }

    [Fact]
    public void PreviewDoesNotRoundAwayFractionalAmount()
    {
        var embed = BudgetCancellationEmbeds.Confirmation(Preview() with { Amount = 5000.50m });
        Assert.Contains("5,000.50", string.Join("\n", embed.Fields.Select(f => f.Value)));
    }
}
