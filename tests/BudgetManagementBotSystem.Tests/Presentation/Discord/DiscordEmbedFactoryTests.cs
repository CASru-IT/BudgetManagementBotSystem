using BudgetManagementBotSystem.Application.DTOs;
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
}
