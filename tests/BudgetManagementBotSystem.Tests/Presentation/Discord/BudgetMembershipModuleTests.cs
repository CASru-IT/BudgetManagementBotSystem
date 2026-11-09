using System.Runtime.CompilerServices;
using BudgetManagementBotSystem.Application.Interface;
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

    [Theory]
    [InlineData("add-budget", "追加予算")]
    [InlineData("income", "収入")]
    [InlineData("expense", "支出")]
    public async Task SavedBudgetEntryPostsItsKindActorGroupAndAmount(string operation, string label)
    {
        var fixture = new Fixture(1);
        fixture.User.ChangeRole(AccountRole.Admin);
        fixture.UnitOfWork.Setup(u => u.SaveChangesAsync()).Returns(() =>
        {
            Assert.Empty(fixture.Activity.Posts);
            typeof(BudgetTransaction).GetProperty(nameof(BudgetTransaction.Id))!
                .SetValue(fixture.Group.BudgetTransactions.Last(), 71);
            return Task.CompletedTask;
        });
        if (operation == "add-budget")
            await fixture.Module.AddBudget(10, 125);
        else
            await fixture.Module.AdminAddTransaction(10, operation, 125);

        var post = Assert.Single(fixture.Activity.Posts);
        Assert.Contains(label, post.Title);
        Assert.Contains(post.Fields, f => f.Value.Contains("対象班"));
        Assert.Contains(post.Fields, f => f.Value.Contains("125"));
        Assert.Contains(post.Fields, f => f.Value == "<@0>");
        Assert.Contains("#71", post.Footer?.Text);
        Assert.True(fixture.Module.Ephemeral);
        fixture.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task NotificationDisplaysAmountAtTheLedgerPrecision()
    {
        var fixture = new Fixture(1);
        fixture.User.ChangeRole(AccountRole.Admin);
        await fixture.Module.AddBudget(10, 1234.565);
        var post = Assert.Single(fixture.Activity.Posts);
        Assert.Contains(post.Fields, f => f.Name == "金額" && f.Value == "1,234.57 円");
    }

    [Fact]
    public async Task FailedSaveDoesNotPostBudgetEntry()
    {
        var fixture = new Fixture(1);
        fixture.User.ChangeRole(AccountRole.Admin);
        fixture.UnitOfWork.Setup(u => u.SaveChangesAsync()).ThrowsAsync(new IOException("save failed"));
        await fixture.Module.AddBudget(10, 125);
        Assert.Empty(fixture.Activity.Posts);
        Assert.Contains("処理できません", fixture.Module.Response!.Title);
    }

    [Fact]
    public async Task UnavailableChannelStillConfirmsSavedTransaction()
    {
        var fixture = new Fixture(1);
        fixture.User.ChangeRole(AccountRole.Admin);
        fixture.Activity.Client.Setup(c => c.GetChannelAsync(RecordingBudgetChannel.ChannelId, CacheMode.AllowDownload, null))
            .ThrowsAsync(new IOException("Discord unavailable"));
        await fixture.Module.AdminAddTransaction(10, "income", 125);
        Assert.Contains("追加しました", fixture.Module.Response!.Title);
        fixture.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        fixture.Activity.Client.Verify(c => c.GetChannelAsync(RecordingBudgetChannel.ChannelId, CacheMode.AllowDownload, null), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("not-a-channel-id")]
    public async Task WithoutAValidDestinationAddsBudgetWithoutContactingDiscord(string? setting)
    {
        var fixture = new Fixture(1, new RecordingBudgetChannel(setting));
        fixture.User.ChangeRole(AccountRole.President);
        await fixture.Module.AddBudget(10, 50);
        Assert.Contains("追加しました", fixture.Module.Response!.Title);
        Assert.Empty(fixture.Activity.Client.Invocations);
        fixture.UnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    private sealed class Fixture
    {
        public User User { get; } = new("利用者", 0, AccountRole.GroupLeader);
        public PagingSessionStore Store { get; } = new();
        public RecordingBudgetChannel Activity { get; }
        public Group Group { get; } = new("対象班");
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public RecordingModule Module { get; }
        public Fixture(int membershipCount, RecordingBudgetChannel? activity = null)
        {
            Activity = activity ?? new RecordingBudgetChannel();
            for (var i = 0; i < membershipCount; i++) User.AddGroup(10 + i);
            var users = new Mock<IUserRepository>();
            users.Setup(r => r.GetByDiscordUserIdAsync(0)).ReturnsAsync(User);
            typeof(Group).GetProperty("Id")!.SetValue(Group, 10);
            Group.AddBudgetTransaction(new BudgetTransaction(true, 1000, new FiscalYear(4)));
            Group.AddBudgetTransaction(new BudgetTransaction(true, 1000, new FiscalYear(4)));
            var groups = new Mock<IGroupRepository>();
            groups.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(Group);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FiscalYearStartMonth:Month"] = "4" }).Build();
            Module = new RecordingModule(users.Object, new BudgetQueryUseCase(groups.Object, users.Object, configuration),
                new IncreaseBudgetLimitUseCase(groups.Object, configuration, UnitOfWork.Object),
                new AdminAddBudgetTransactionUseCase(users.Object, groups.Object, configuration, UnitOfWork.Object), Store, Activity.Service);
        }
    }

    private sealed class RecordingModule : BudgetModule
    {
        public Embed? Response { get; private set; }
        public MessageComponent? Components { get; private set; }
        public bool Ephemeral { get; private set; }
        public RecordingModule(IUserRepository users, BudgetQueryUseCase query, IncreaseBudgetLimitUseCase increase,
            AdminAddBudgetTransactionUseCase direct, PagingSessionStore store, BudgetActivityChannel activityChannel)
            : base(users, query, increase, direct, store, NullLogger<BudgetModule>.Instance, activityChannel)
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

        protected override Task DeferAsync(bool ephemeral = false, RequestOptions? options = null)
        { Assert.True(ephemeral); return Task.CompletedTask; }

        protected override Task<IUserMessage> FollowupAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
            bool ephemeral = false, AllowedMentions? allowedMentions = null, RequestOptions? options = null,
            MessageComponent? components = null, Embed? embed = null, PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        { Response = embed; Ephemeral = ephemeral; return Task.FromResult<IUserMessage>(null!); }
    }
}
