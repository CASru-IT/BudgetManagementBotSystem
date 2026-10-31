using System.Runtime.CompilerServices;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Exceptions;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Presentation.Discord.Models;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using BudgetManagementBotSystem.Presentation.Discord.Modules;
using BudgetManagementBotSystem.Presentation.Discord.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class RequestMembershipFeedbackTests
{
    [Theory]
    [InlineData(11)]
    [InlineData(null)]
    public async Task CreateRequest_ExplainsMembershipBeforeReadingAttachments(int? membership)
    {
        var fixture = new Fixture(membership);
        var module = new RecordingCreateModule(fixture);
        // Any attachment access is a failure: membership must be checked first.
        var attachment = new Mock<IAttachment>(MockBehavior.Strict);

        await module.CreateRequest(10, 100, "備品購入", attachment.Object);

        AssertFeedback(module.Response, membership);
        Assert.Null(module.Components);
        Assert.Empty(fixture.Group.Requests);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(null)]
    public async Task ConfirmRequest_RechecksCurrentMembershipAndExplainsWithoutSaving(int? membership)
    {
        var fixture = new Fixture(10);
        var token = fixture.Store.Create(new PendingRequestConfirmation
        {
            RequesterDiscordUserId = 0,
            UserId = fixture.User.Id,
            GroupId = 10,
            GroupName = fixture.Group.Name,
            Amount = 100,
            Description = "備品購入",
            EvidenceFiles = new List<UploadedEvidenceDto> { new("receipt.pdf", new byte[] { 1, 2, 3 }) }
        });
        fixture.User.ChangeGroupId(membership);
        var module = new RecordingConfirmModule(fixture);

        await module.ConfirmCreateRequest(token);

        AssertFeedback(module.Response, membership);
        fixture.Files.Verify(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Never);
        Assert.Empty(fixture.Group.Requests);
    }

    [Theory]
    [InlineData(AccountRole.GroupLeader)]
    [InlineData(AccountRole.Accountant)]
    [InlineData(AccountRole.President)]
    [InlineData(AccountRole.Admin)]
    public async Task SubmitRequest_RejectsOtherGroupBeforeEvidenceStorageForEveryRole(AccountRole role)
    {
        var fixture = new Fixture(11);
        fixture.User.ChangeRole(role);

        await Assert.ThrowsAsync<BudgetRequestMembershipException>(() => fixture.Submit.ExecuteAsync(
            fixture.User.Id, 10, 100, "備品購入", new[] { new UploadedEvidenceDto("receipt.pdf", new byte[] { 1 }) }));

        fixture.Files.Verify(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Never);
        Assert.Empty(fixture.Group.Requests);
        Assert.Equal(11, fixture.User.GroupId);
    }

    [Fact]
    public async Task SubmitRequest_OwnGroupStillCreatesPendingRequestWithEvidence()
    {
        var fixture = new Fixture(10);
        var result = await fixture.Submit.ExecuteAsync(fixture.User.Id, 10, 100, "備品購入",
            new[] { new UploadedEvidenceDto("receipt.pdf", new byte[] { 1, 2, 3 }) });
        var request = Assert.Single(fixture.Group.Requests);
        Assert.Equal(fixture.User.Id, request.UserId);
        Assert.Equal(RequestStatus.Pending, request.GetCurrentStatus());
        Assert.Equal(1, result.SavedEvidenceCount);
        Assert.Single(request.Evidences);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    private static void AssertFeedback(Embed? embed, int? membership)
    {
        Assert.NotNull(embed);
        Assert.Equal("所属班を確認してください", embed.Title);
        Assert.Contains("執行部", embed.Description);
        Assert.Contains("班ID: 10", embed.Description);
        Assert.Contains(membership.HasValue ? "班ID: 11" : "未設定", embed.Description);
        Assert.Contains("/assign-group", embed.Description);
        Assert.Contains("申請は作成されていません", embed.Description);
        Assert.DoesNotContain("時間を置いて", embed.Description);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Feedback_WhenGroupNameUnavailable_StillExplainsKnownReason(bool lookupFails)
    {
        var groups = new Mock<IGroupRepository>();
        if (lookupFails)
            groups.Setup(x => x.GetByIdAsync(11)).ThrowsAsync(new IOException("private diagnostic"));
        else
            groups.Setup(x => x.GetByIdAsync(11)).ReturnsAsync((Group?)null);

        var embed = await RequestMembershipFeedback.BuildAsync(
            new BudgetRequestMembershipException(11, 10, "執行部"), groups.Object, NullLogger.Instance);

        AssertFeedback(embed, 11);
        Assert.DoesNotContain("private diagnostic", embed.Description);
    }

    [Fact]
    public async Task Feedback_IncludesRegisteredGroupNameWhenAvailable()
    {
        var fixture = new Fixture(11);
        var module = new RecordingCreateModule(fixture);
        await module.CreateRequest(10, 100, "備品購入", new Mock<IAttachment>(MockBehavior.Strict).Object);
        Assert.Contains("ボードゲーム", module.Response!.Description);
    }

    private sealed class Fixture
    {
        public User User { get; }
        public Group Group { get; } = new("執行部");
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IGroupRepository> Groups { get; } = new();
        public Mock<IFileStorage> Files { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public PendingRequestConfirmationStore Store { get; } = new();
        public SubmitBudgetRequestUseCase Submit { get; }

        public Fixture(int? membership)
        {
            User = new User("Requester", 0, AccountRole.GroupLeader, membership);
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(User, 13);
            typeof(Group).GetProperty(nameof(Group.Id))!.SetValue(Group, 10);
            Group.AddBudgetTransaction(new BudgetTransaction(true, 1000, new FiscalYear(4)));
            var ownGroup = new Group("ボードゲーム");
            typeof(Group).GetProperty(nameof(Group.Id))!.SetValue(ownGroup, 11);
            Users.Setup(x => x.GetByIdAsync(13)).ReturnsAsync(User);
            Users.Setup(x => x.GetByDiscordUserIdAsync(0)).ReturnsAsync(User);
            Groups.Setup(x => x.GetByIdAsync(10)).ReturnsAsync(Group);
            Groups.Setup(x => x.GetByIdAsync(11)).ReturnsAsync(ownGroup);
            Files.Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>())).ReturnsAsync("stored/receipt.pdf");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["FiscalYearStartMonth:Month"] = "4" }).Build();
            Submit = new SubmitBudgetRequestUseCase(Users.Object, Groups.Object, configuration, UnitOfWork.Object, Files.Object);
        }
    }

    private static void SetContext(InteractionModuleBase<SocketInteractionContext> module)
    {
        var interaction = (SocketSlashCommand)RuntimeHelpers.GetUninitializedObject(typeof(SocketSlashCommand));
        var user = (SocketSelfUser)RuntimeHelpers.GetUninitializedObject(typeof(SocketSelfUser));
        typeof(SocketInteraction).GetProperty(nameof(SocketInteraction.User))!.SetValue(interaction, user);
        typeof(InteractionModuleBase<SocketInteractionContext>).GetProperty(nameof(module.Context))!
            .SetValue(module, new SocketInteractionContext(null!, interaction));
    }

    private sealed class RecordingCreateModule : RequestModule
    {
        public Embed? Response { get; private set; }
        public MessageComponent? Components { get; private set; }
        public RecordingCreateModule(Fixture f) : base(null!, null!, f.Users.Object, f.Groups.Object, null!, null!, null!, null!, f.Store, null!, NullLogger<RequestModule>.Instance) => SetContext(this);
        protected override Task DeferAsync(bool ephemeral = false, RequestOptions? options = null)
        { Assert.True(ephemeral); return Task.CompletedTask; }
        protected override Task<IUserMessage> FollowupAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
            bool ephemeral = false, AllowedMentions? allowedMentions = null, RequestOptions? options = null,
            MessageComponent? components = null, Embed? embed = null, PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        { Assert.True(ephemeral); Response = embed; Components = components; return Task.FromResult<IUserMessage>(null!); }
    }

    private sealed class RecordingConfirmModule : RequestComponentModule
    {
        public Embed? Response { get; private set; }
        public RecordingConfirmModule(Fixture f) : base(f.Submit, f.Users.Object, f.Store, null!, null!, NullLogger<RequestComponentModule>.Instance, f.Groups.Object) => SetContext(this);
        protected override Task DeferAsync(bool ephemeral = false, RequestOptions? options = null)
        { Assert.True(ephemeral); return Task.CompletedTask; }
        protected override Task<IUserMessage> FollowupAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
            bool ephemeral = false, AllowedMentions? allowedMentions = null, RequestOptions? options = null,
            MessageComponent? components = null, Embed? embed = null, PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        { Assert.True(ephemeral); Response = embed; return Task.FromResult<IUserMessage>(null!); }
    }
}
