using System.Runtime.CompilerServices;
using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Presentation.Discord.Modules;
using BudgetManagementBotSystem.Presentation.Discord.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class BudgetMembershipModuleTests
{
    [Theory]
    [InlineData(nameof(BudgetModule.RemainingBudget))]
    [InlineData(nameof(BudgetModule.UsageHistory))]
    public void BudgetCommands_GroupArgumentIsOptional(string method)
    {
        var parameter = typeof(BudgetModule).GetMethod(method)!.GetParameters()[0];
        Assert.True(parameter.IsOptional);
        Assert.Equal(typeof(int?), parameter.ParameterType);
        Assert.Null(parameter.DefaultValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task OmittedGroup_ExplainsSelectionPrivately(int membershipCount)
    {
        var fixture = new Fixture(membershipCount);
        await fixture.Module.RemainingBudget();
        Assert.True(fixture.Module.Ephemeral);
        Assert.Contains("group-id", fixture.Module.Response!.Description);
        await fixture.Module.UsageHistory();
        Assert.True(fixture.Module.Ephemeral);
        Assert.Contains("group-id", fixture.Module.Response!.Description);
    }

    [Fact]
    public async Task SingleMembership_DefaultsAndPinsHistoryToResolvedGroup()
    {
        var fixture = new Fixture(1);
        await fixture.Module.RemainingBudget();
        Assert.Contains(fixture.Module.Response!.Fields, f => f.Value.Contains("対象班"));
        await fixture.Module.UsageHistory(pageSize: 1);
        var row = Assert.IsType<ActionRowComponent>(fixture.Module.Components!.Components.First());
        var button = Assert.IsType<ButtonComponent>(row.Components.First());
        var token = button.CustomId.Split(':')[2];
        Assert.True(fixture.Store.TryGet(token, out var session));
        Assert.Equal(10, session!.GroupId);
        fixture.User.RemoveGroup(10);
        fixture.User.AddGroup(11);
        Assert.Equal(10, session.GroupId);
    }

    private sealed class Fixture
    {
        public User User { get; } = new("利用者", 0, AccountRole.GroupLeader);
        public PagingSessionStore Store { get; } = new();
        public RecordingModule Module { get; }
        public Fixture(int membershipCount)
        {
            for (var i = 0; i < membershipCount; i++) User.AddGroup(10 + i);
            var users = new Mock<IUserRepository>();
            users.Setup(r => r.GetByDiscordUserIdAsync(0)).ReturnsAsync(User);
            var group = new Group("対象班");
            typeof(Group).GetProperty("Id")!.SetValue(group, 10);
            group.AddBudgetTransaction(new BudgetTransaction(true, 1000, new FiscalYear(4)));
            group.AddBudgetTransaction(new BudgetTransaction(true, 1000, new FiscalYear(4)));
            var groups = new Mock<IGroupRepository>();
            groups.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(group);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FiscalYearStartMonth:Month"] = "4" }).Build();
            Module = new RecordingModule(new BudgetQueryUseCase(groups.Object, users.Object, configuration), Store);
        }
    }

    private sealed class RecordingModule : BudgetModule
    {
        public Embed? Response { get; private set; }
        public MessageComponent? Components { get; private set; }
        public bool Ephemeral { get; private set; }
        public RecordingModule(BudgetQueryUseCase query, PagingSessionStore store)
            : base(null!, query, null!, null!, store, NullLogger<BudgetModule>.Instance)
        {
            var interaction = (SocketSlashCommand)RuntimeHelpers.GetUninitializedObject(typeof(SocketSlashCommand));
            var user = (SocketSelfUser)RuntimeHelpers.GetUninitializedObject(typeof(SocketSelfUser));
            typeof(SocketInteraction).GetProperty(nameof(SocketInteraction.User))!.SetValue(interaction, user);
            typeof(InteractionModuleBase<SocketInteractionContext>).GetProperty(nameof(Context))!
                .SetValue(this, new SocketInteractionContext(null!, interaction));
        }
        protected override Task RespondAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
            bool ephemeral = false, AllowedMentions? allowedMentions = null, RequestOptions? options = null,
            MessageComponent? components = null, Embed? embed = null, PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        { Response = embed; Components = components; Ephemeral = ephemeral; return Task.CompletedTask; }
    }
}
