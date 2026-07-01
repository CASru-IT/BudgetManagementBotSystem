using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class DiscordEmbedFactoryTests
{
    [Fact]
    public void BuildSuccessEmbed_UsesGreen()
    {
        var embed = DiscordEmbedFactory.BuildSuccessEmbed("完了", "処理が完了しました。");

        Assert.Equal("完了", embed.Title);
        Assert.Equal(Color.Green, embed.Color);
    }

    [Fact]
    public void BuildErrorEmbed_UsesRed()
    {
        var embed = DiscordEmbedFactory.BuildErrorEmbed("失敗", "処理できません。");

        Assert.Equal("失敗", embed.Title);
        Assert.Equal(Color.Red, embed.Color);
    }

    [Fact]
    public void BuildUsageHistoryEmbed_EmptyResult_ShowsEmptyDescriptionAndPaging()
    {
        var result = new PagedResult<TransactionDto>
        {
            Total = 0,
            Page = 1,
            PageSize = 10,
            ResolvedFiscalYear = 2026
        };

        var embed = DiscordEmbedFactory.BuildUsageHistoryEmbed(result);

        Assert.Equal("予算使用履歴", embed.Title);
        Assert.Contains("条件に一致する取引履歴はありません", embed.Description);
        Assert.Contains(embed.Fields, field => field.Name == "ページ" && field.Value == "1/1");
    }

    [Fact]
    public void BuildRequestListEmbed_TruncatesLongDescription()
    {
        var result = new PagedResult<PendingRequestDto>
        {
            Total = 1,
            Page = 1,
            PageSize = 10,
            Items =
            {
                new PendingRequestDto
                {
                    Id = 12,
                    GroupName = "ゲーム班",
                    GroupId = 1,
                    Amount = 4500m,
                    RequestDate = new DateTime(2026, 7, 1),
                    Description = new string('あ', 120)
                }
            }
        };

        var embed = DiscordEmbedFactory.BuildRequestListEmbed(result, null);

        Assert.Single(embed.Fields.Where(field => field.Name.Contains("#12")));
        Assert.Contains("...", embed.Fields.Single(field => field.Name.Contains("#12")).Value);
    }

    [Fact]
    public void BuildRequestListEmbed_UsesDisplayedItemCountInFooter()
    {
        var result = new PagedResult<PendingRequestDto>
        {
            Total = 50,
            Page = 1,
            PageSize = 50
        };

        for (var i = 1; i <= 50; i++)
        {
            result.Items.Add(CreatePendingRequest(i));
        }

        var embed = DiscordEmbedFactory.BuildRequestListEmbed(result, null);

        Assert.Equal(20, embed.Fields.Count(field => field.Name.Contains("#")));
        Assert.Contains("20/50", embed.Footer?.Text);
    }

    [Fact]
    public void BuildPendingRequestsEmbed_UsesDisplayedItemCountInFooter()
    {
        var result = new PagedResult<PendingRequestDto>
        {
            Total = 50,
            Page = 1,
            PageSize = 50
        };

        for (var i = 1; i <= 50; i++)
        {
            result.Items.Add(CreatePendingRequest(i));
        }

        var embed = DiscordEmbedFactory.BuildPendingRequestsEmbed(result);

        Assert.Equal(20, embed.Fields.Count(field => field.Name.Contains("#")));
        Assert.Contains("20/50", embed.Footer?.Text);
    }

    [Fact]
    public void BuildAllHistoryEmbed_DoesNotShowLimitWarning_WhenActualCountIsWithinLimit()
    {
        var transactions = Enumerable.Range(1, 3).Select(CreateTransaction).ToList();

        var embed = DiscordEmbedFactory.BuildAllHistoryEmbed(transactions, requestedTake: 50);

        Assert.DoesNotContain("20", embed.Footer?.Text ?? string.Empty);
        Assert.Contains("3/3", embed.Footer?.Text);
    }

    [Fact]
    public void BuildAllHistoryEmbed_ShowsLimitFooter_WhenActualCountExceedsLimit()
    {
        var transactions = Enumerable.Range(1, 25).Select(CreateTransaction).ToList();

        var embed = DiscordEmbedFactory.BuildAllHistoryEmbed(transactions, requestedTake: 50);

        Assert.Equal(20, embed.Fields.Count(field => field.Name.Contains("Group")));
        Assert.Contains("20/25", embed.Footer?.Text);
    }

    private static PendingRequestDto CreatePendingRequest(int id)
    {
        return new PendingRequestDto
        {
            Id = id,
            GroupId = 1,
            GroupName = "Group",
            Amount = id * 1000m,
            RequestDate = new DateTime(2026, 7, 1),
            Description = $"Request {id}",
            Status = RequestStatus.Pending
        };
    }

    private static TransactionDto CreateTransaction(int id)
    {
        return new TransactionDto
        {
            GroupId = 1,
            GroupName = "Group",
            Amount = id * 1000m,
            TransactionDate = new DateTime(2026, 7, 1),
            FiscalYear = 2026,
            IsIncome = false
        };
    }
}
