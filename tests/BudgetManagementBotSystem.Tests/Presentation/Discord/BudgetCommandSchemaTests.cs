using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Presentation.Discord.Autocomplete;
using BudgetManagementBotSystem.Presentation.Discord.Modules;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class BudgetCommandSchemaTests
{
    [Fact]
    public void PublicCommandNamesAreEnglishAndHistoryHasNoDuplicateAlias()
    {
        var commands = new[] {typeof(BudgetModule), typeof(BudgetCancellationModule)}
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttributes(typeof(SlashCommandAttribute), false).Cast<SlashCommandAttribute>().SingleOrDefault())
            .Where(attribute => attribute != null).Select(attribute => attribute!.Name).ToArray();
        Assert.Single(commands, name => name == "budget-history");
        Assert.Contains("cancel-budget", commands);
        Assert.DoesNotContain("usage-history", commands);
        Assert.DoesNotContain("予算履歴", commands);
        Assert.DoesNotContain("予算取消", commands);
        var parameter = typeof(BudgetCancellationModule).GetMethod(nameof(BudgetCancellationModule.CancelBudget))!.GetParameters()[0];
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.Single(parameter.GetCustomAttributes(typeof(AutocompleteAttribute), false));
        Assert.Equal("transaction-id", ((SummaryAttribute)parameter.GetCustomAttributes(typeof(SummaryAttribute), false).Single()).Name);
    }

    [Theory]
    [InlineData(AccountRole.Admin, true)]
    [InlineData(AccountRole.GroupLeader, false)]
    public async Task HandlerReturnsStringIdsAndBoundedLabelsOnlyToAuthorizedUsers(AccountRole role, bool allowed)
    {
        var group = new Group(new string('班', 200));
        var tx = new BudgetTransaction(true, 1234.50m, new FiscalYear(2025, 4));
        typeof(BudgetTransaction).GetProperty("Id")!.SetValue(tx, 123);
        group.AddBudgetTransaction(tx);
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync()).ReturnsAsync([group]);
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByDiscordUserIdAsync(100)).ReturnsAsync(new User("利用者", 100, role));
        using var services = new ServiceCollection().AddSingleton(new CancelBudgetAdditionUseCase(groups.Object, users.Object, Mock.Of<IUnitOfWork>())).BuildServiceProvider();
        var context = new Mock<IInteractionContext>();
        context.Setup(c => c.User.Id).Returns(100);
        var interaction = new Mock<IAutocompleteInteraction>();
        // Discord creates options internally; instantiate its actual payload for the handler boundary.
        var option = (AutocompleteOption)Activator.CreateInstance(typeof(AutocompleteOption),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, [ApplicationCommandOptionType.String, "transaction-id", "班", true], null)!;
        interaction.Setup(i => i.Data.Current).Returns(option);
        var parameter = new Mock<IParameterInfo>();
        parameter.Setup(p => p.ParameterType).Returns(typeof(string));
        var result = await new BudgetTransactionAutocompleteHandler().GenerateSuggestionsAsync(context.Object, interaction.Object, parameter.Object, services);
        Assert.True(result.IsSuccess);
        if (!allowed) { Assert.Empty(result.Suggestions); return; }
        var candidate = Assert.Single(result.Suggestions);
        Assert.Equal("123", candidate.Value);
        Assert.True(candidate.Name.Length <= 100);
        Assert.Contains("1,234.50", candidate.Name);
        Assert.Contains("2025", candidate.Name);
        Assert.Contains("由来未確認", candidate.Name);
        Assert.Contains("班", candidate.Name);
    }
}
