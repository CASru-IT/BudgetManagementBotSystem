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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class BudgetCancellationModuleTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("1.2")]
    [InlineData("999999999999999999999")]
    public async Task InvalidTransactionIdDoesNotCreateConfirmationOrChangeBudget(string id)
    {
        var fixture = new Fixture();
        await fixture.Module.CancelBudget(id, "訂正");
        Assert.Single(fixture.Group.BudgetTransactions);
        Assert.Null(fixture.Module.Components);
        Assert.Contains("取引ID", fixture.Module.LastEmbed!.Description);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ButtonsOnlyWriteOnConfirmation_AndCannotRepeat(bool confirm)
    {
        var fixture = new Fixture();
        await fixture.Module.CancelBudget("123", "班の訂正");
        Assert.True(fixture.Module.Deferred);
        Assert.Single(fixture.Group.BudgetTransactions);
        var token = fixture.Module.Components!.Components.OfType<ActionRowComponent>().First().Components.OfType<ButtonComponent>().First().CustomId.Split(':').Last();
        if (confirm) await fixture.Module.ConfirmCancellation(token);
        else await fixture.Module.AbortCancellation(token);
        Assert.Equal(confirm ? 2 : 1, fixture.Group.BudgetTransactions.Count);
        Assert.Empty(fixture.Module.Components!.Components);
        await fixture.Module.ConfirmCancellation(token);
        Assert.Equal(confirm ? 2 : 1, fixture.Group.BudgetTransactions.Count);
    }

    [Fact]
    public async Task AcknowledgesBeforeDatabaseRead_AndHidesInternalErrors()
    {
        var fixture = new Fixture();
        var pending = new TaskCompletionSource<List<Group>?>();
        fixture.Groups.Setup(x => x.GetAllAsync()).Returns(pending.Task);
        var running = fixture.Module.CancelBudget("123", "訂正");
        Assert.True(fixture.Module.Deferred);
        pending.SetException(new InvalidOperationException("private database configuration"));
        await running;
        Assert.DoesNotContain("private database", fixture.Module.LastEmbed!.Description);
    }

    [Fact]
    public async Task LostPermissionsAtConfirmationDoNotWrite()
    {
        var fixture = new Fixture();
        await fixture.Module.CancelBudget("123", "訂正");
        var token = fixture.Module.Components!.Components.OfType<ActionRowComponent>().First().Components.OfType<ButtonComponent>().First().CustomId.Split(':').Last();
        fixture.User.Deactivate();
        await fixture.Module.ConfirmCancellation(token);
        Assert.Single(fixture.Group.BudgetTransactions);
        Assert.Contains("権限", fixture.Module.LastEmbed!.Title);
    }

    private sealed class Fixture
    {
        public Group Group { get; } = new("ハリボテ班");
        public BudgetManagementBotSystem.Domain.Entities.User User { get; } = new("会計", 0, AccountRole.Accountant);
        public Mock<IGroupRepository> Groups { get; } = new();
        public RecordingModule Module { get; }
        public Fixture()
        {
            var addition = new BudgetTransaction(true, 5000, new FiscalYear(2025, 4), BudgetTransactionKind.BudgetAddition);
            typeof(BudgetTransaction).GetProperty(nameof(BudgetTransaction.Id))!.SetValue(addition, 123);
            Group.AddBudgetTransaction(addition);
            Groups.Setup(x => x.GetAllAsync()).ReturnsAsync([Group]);
            var users = new Mock<IUserRepository>();
            users.Setup(x => x.GetByDiscordUserIdAsync(0)).ReturnsAsync(User);
            var unit = new Mock<IUnitOfWork>();
            unit.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);
            Module = new(new(Groups.Object, users.Object, unit.Object), new());
        }
    }

    private sealed class RecordingModule : BudgetCancellationModule
    {
        public bool Deferred { get; private set; }
        public Embed? LastEmbed { get; private set; }
        public MessageComponent? Components { get; private set; }
        public RecordingModule(CancelBudgetAdditionUseCase uc, BudgetCancellationConfirmationStore store)
            : base(uc, store, NullLogger<BudgetCancellationModule>.Instance)
        {
            var interaction = (SocketSlashCommand)RuntimeHelpers.GetUninitializedObject(typeof(SocketSlashCommand));
            var user = (SocketSelfUser)RuntimeHelpers.GetUninitializedObject(typeof(SocketSelfUser));
            typeof(SocketInteraction).GetProperty(nameof(SocketInteraction.User))!.SetValue(interaction, user);
            typeof(InteractionModuleBase<SocketInteractionContext>).GetProperty(nameof(Context))!
                .SetValue(this, new SocketInteractionContext(null!, interaction));
        }
        protected override Task DeferAsync(bool ephemeral = false, RequestOptions? options = null)
        {
            Assert.True(ephemeral);
            Deferred = true;
            return Task.CompletedTask;
        }
        protected override Task<IUserMessage> FollowupAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
            bool ephemeral = false, AllowedMentions? allowedMentions = null, RequestOptions? options = null,
            MessageComponent? components = null, Embed? embed = null, PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        {
            Assert.True(Deferred);
            Assert.True(ephemeral);
            LastEmbed = embed;
            Components = components;
            return Task.FromResult<IUserMessage>(null!);
        }
        protected override Task<IUserMessage> ModifyOriginalResponseAsync(Action<MessageProperties> func, RequestOptions? options = null)
        {
            Assert.True(Deferred);
            var properties = new MessageProperties();
            func(properties);
            LastEmbed = properties.Embed.Value;
            Components = properties.Components.Value;
            return Task.FromResult<IUserMessage>(null!);
        }
    }
}
