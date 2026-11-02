using System.Runtime.CompilerServices;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Presentation.Discord.Modules;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class RequestModuleTests
{
    [Fact]
    public async Task RequestDetail_AcknowledgesBeforeWaitingForData()
    {
        var fixture = new Fixture();
        var pendingRead = new TaskCompletionSource<List<Group>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Groups.Setup(repository => repository.GetAllAsync()).Returns(pendingRead.Task);

        var execution = fixture.Module.RequestDetail("37");
        var acknowledgedWhileWaiting = fixture.Module.Deferred;
        pendingRead.SetResult(new List<Group> { fixture.Group });
        await execution;

        Assert.True(acknowledgedWhileWaiting, "Discord must be acknowledged before database or file I/O can exceed the initial response deadline.");
        Assert.Equal("申請詳細 #37", fixture.Module.LastEmbed?.Title);
        Assert.Equal(1, fixture.Module.FollowupCount);
        Assert.Equal(0, fixture.Module.InitialResponseCount);
    }

    [Theory]
    [InlineData(AccountRole.Accountant, true)]
    [InlineData(AccountRole.GroupLeader, false)]
    public async Task RequestDetail_WithEvidence_UsesFollowupAndPreservesFilesAndButtons(AccountRole role, bool hasButtons)
    {
        var fixture = new Fixture(withEvidence: true, role);
        var pendingFile = new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Files.Setup(storage => storage.GetFileAsync("receipt.pdf")).Returns(pendingFile.Task);

        var execution = fixture.Module.RequestDetail("37");
        var acknowledgedWhileWaiting = fixture.Module.Deferred;
        pendingFile.SetResult(new MemoryStream(new byte[] { 1, 2, 3 }));
        await execution;

        Assert.True(acknowledgedWhileWaiting);
        Assert.Equal(1, fixture.Module.FollowupCount);
        Assert.Equal(0, fixture.Module.InitialResponseCount);
        Assert.False(fixture.Module.LastEphemeral);
        Assert.Equal("申請詳細 #37", fixture.Module.LastEmbed?.Title);
        var attachment = Assert.Single(fixture.Module.Uploads);
        Assert.Equal("receipt.pdf", attachment.Name);
        Assert.Equal(new byte[] { 1, 2, 3 }, attachment.Content);
        Assert.Equal(hasButtons, fixture.Module.LastComponents != null);
        Assert.All(fixture.Module.UploadedStreams, stream => Assert.False(stream.CanRead));
    }

    [Fact]
    public async Task RequestDetail_WithoutEvidence_UsesFollowup()
    {
        var fixture = new Fixture();

        await fixture.Module.RequestDetail("37");

        Assert.True(fixture.Module.Deferred);
        Assert.Equal(1, fixture.Module.FollowupCount);
        Assert.Equal(0, fixture.Module.InitialResponseCount);
        Assert.Equal("申請詳細 #37", fixture.Module.LastEmbed?.Title);
        Assert.Empty(fixture.Module.Uploads);
    }

    [Fact]
    public async Task RequestDetail_UnknownId_CompletesDeferredResponse()
    {
        var fixture = new Fixture();

        await fixture.Module.RequestDetail("999");

        Assert.True(fixture.Module.Deferred);
        Assert.Equal(1, fixture.Module.FollowupCount);
        Assert.Equal(0, fixture.Module.InitialResponseCount);
        Assert.Contains("999", fixture.Module.LastEmbed?.Description);
    }

    [Fact]
    public async Task RequestDetail_DataFailure_CompletesDeferredResponseWithSafeError()
    {
        var fixture = new Fixture();
        fixture.Groups.Setup(repository => repository.GetAllAsync()).ThrowsAsync(new IOException("private diagnostic"));

        await fixture.Module.RequestDetail("37");

        Assert.True(fixture.Module.Deferred);
        Assert.Equal(1, fixture.Module.FollowupCount);
        Assert.Equal(0, fixture.Module.InitialResponseCount);
        Assert.Equal("申請詳細を取得できません", fixture.Module.LastEmbed?.Title);
        Assert.DoesNotContain("private diagnostic", fixture.Module.LastEmbed?.Description);
    }

    [Fact]
    public async Task RequestDetail_InvalidId_RespondsPrivatelyWithoutReadingData()
    {
        var fixture = new Fixture();

        await fixture.Module.RequestDetail("invalid");

        Assert.False(fixture.Module.Deferred);
        Assert.Equal(1, fixture.Module.InitialResponseCount);
        Assert.Equal(0, fixture.Module.FollowupCount);
        Assert.True(fixture.Module.LastEphemeral);
        fixture.Groups.Verify(repository => repository.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task RequestDetail_UploadFailure_UsesFollowupErrorAndDisposesFiles()
    {
        var fixture = new Fixture(withEvidence: true);
        fixture.Files.Setup(storage => storage.GetFileAsync("receipt.pdf"))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        fixture.Module.FailUpload = true;

        await fixture.Module.RequestDetail("37");

        Assert.True(fixture.Module.Deferred);
        Assert.Equal(2, fixture.Module.FollowupCount);
        Assert.Equal(0, fixture.Module.InitialResponseCount);
        Assert.Equal("申請詳細を取得できません", fixture.Module.LastEmbed?.Title);
        Assert.Single(fixture.Module.UploadedStreams);
        Assert.All(fixture.Module.UploadedStreams, stream => Assert.False(stream.CanRead));
    }

    private sealed class Fixture
    {
        public Mock<IGroupRepository> Groups { get; } = new();
        public Mock<IFileStorage> Files { get; } = new();
        public Group Group { get; } = new("Test Group");
        public RecordingRequestModule Module { get; }

        public Fixture(bool withEvidence = false, AccountRole role = AccountRole.GroupLeader)
        {
            var user = new User("Requester", 0UL, role);
            user.AddGroup(Group.Id);
            var request = Group.CreateBudgetRequest(user, new Money(1000m), new FiscalYear(4), "備品購入",
                withEvidence ? new[] { "receipt.pdf" } : Array.Empty<string>());
            typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, 37);
            Groups.Setup(repository => repository.GetAllAsync()).ReturnsAsync(new List<Group> { Group });
            var users = new Mock<IUserRepository>();
            users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
            users.Setup(repository => repository.GetByDiscordUserIdAsync(0UL)).ReturnsAsync(user);
            Module = new RecordingRequestModule(users.Object, new RequestDetailUseCase(Groups.Object, users.Object, Files.Object));
        }
    }

    // Record only the Discord transport boundary; execute the real module, use case and embed/component builders.
    private sealed class RecordingRequestModule : RequestModule
    {
        public bool Deferred { get; private set; }
        public int InitialResponseCount { get; private set; }
        public int FollowupCount { get; private set; }
        public Embed? LastEmbed { get; private set; }
        public MessageComponent? LastComponents { get; private set; }
        public bool LastEphemeral { get; private set; }
        public List<(string Name, byte[] Content)> Uploads { get; } = new();
        public List<Stream> UploadedStreams { get; } = new();
        public bool FailUpload { get; set; }

        public RecordingRequestModule(IUserRepository users, RequestDetailUseCase details)
            : base(null!, null!, users, null!, null!, null!, details, null!, null!, null!, NullLogger<RequestModule>.Instance)
        {
            // Discord socket entities have internal constructors. Only the invoking user's ID is needed here.
            var interaction = (SocketSlashCommand)RuntimeHelpers.GetUninitializedObject(typeof(SocketSlashCommand));
            var user = (SocketSelfUser)RuntimeHelpers.GetUninitializedObject(typeof(SocketSelfUser));
            typeof(SocketInteraction).GetProperty(nameof(SocketInteraction.User))!.SetValue(interaction, user);
            var context = new SocketInteractionContext(null!, interaction);
            typeof(InteractionModuleBase<SocketInteractionContext>).GetProperty(nameof(Context))!.SetValue(this, context);
        }

        protected override Task DeferAsync(bool ephemeral = false, RequestOptions? options = null)
        {
            Assert.False(Deferred);
            Assert.Equal(0, InitialResponseCount);
            Assert.False(ephemeral, "Successful details must remain public; the first followup inherits deferral visibility.");
            Deferred = true;
            return Task.CompletedTask;
        }

        protected override Task RespondAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
            bool ephemeral = false, AllowedMentions? allowedMentions = null, RequestOptions? options = null,
            MessageComponent? components = null, Embed? embed = null, PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        {
            Assert.False(Deferred, "A deferred interaction cannot receive a second initial response.");
            InitialResponseCount++;
            Record(embeds, embed, components, ephemeral);
            return Task.CompletedTask;
        }

        protected override async Task RespondWithFilesAsync(IEnumerable<FileAttachment> attachments, string? text = null,
            Embed[]? embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions? allowedMentions = null,
            MessageComponent? components = null, Embed? embed = null, RequestOptions? options = null,
            PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        {
            Assert.False(Deferred);
            InitialResponseCount++;
            Record(embeds, embed, components, ephemeral);
            await CaptureFiles(attachments);
        }

        protected override Task<IUserMessage> FollowupAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false,
            bool ephemeral = false, AllowedMentions? allowedMentions = null, RequestOptions? options = null,
            MessageComponent? components = null, Embed? embed = null, PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        {
            Assert.True(Deferred);
            FollowupCount++;
            Record(embeds, embed, components, ephemeral);
            return Task.FromResult<IUserMessage>(null!);
        }

        protected override async Task<IUserMessage> FollowupWithFilesAsync(IEnumerable<FileAttachment> attachments, string? text = null,
            Embed[]? embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions? allowedMentions = null,
            MessageComponent? components = null, Embed? embed = null, RequestOptions? options = null,
            PollProperties? poll = null, MessageFlags flags = MessageFlags.None)
        {
            Assert.True(Deferred);
            FollowupCount++;
            Record(embeds, embed, components, ephemeral);
            await CaptureFiles(attachments);
            if (FailUpload)
            {
                throw new IOException("Upload failed");
            }
            return null!;
        }

        private void Record(Embed[]? embeds, Embed? embed, MessageComponent? components, bool ephemeral)
        {
            LastEmbed = embed ?? embeds?.Single();
            LastComponents = components;
            LastEphemeral = ephemeral;
        }

        private async Task CaptureFiles(IEnumerable<FileAttachment> attachments)
        {
            foreach (var attachment in attachments)
            {
                UploadedStreams.Add(attachment.Stream);
                using var copy = new MemoryStream();
                await attachment.Stream.CopyToAsync(copy);
                Uploads.Add((attachment.FileName, copy.ToArray()));
            }
        }
    }
}
