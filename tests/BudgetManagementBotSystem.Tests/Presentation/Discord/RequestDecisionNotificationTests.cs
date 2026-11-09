using System.Runtime.CompilerServices;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.Presentation.Discord.Models;
using BudgetManagementBotSystem.Presentation.Discord.Modules;
using BudgetManagementBotSystem.Presentation.Discord.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class RequestDecisionNotificationTests
{
    [Theory]
    [InlineData(true, "承認", RequestStatus.Approved)]
    [InlineData(false, "却下", RequestStatus.Rejected)]
    public async Task SavedDecisionPostsItsOutcomeOnce(bool approve, string label, RequestStatus status)
    {
        var fixture = new Fixture();
        fixture.UnitOfWork.Setup(w => w.SaveChangesAsync()).Returns(() =>
        {
            Assert.Empty(fixture.Activity.Client.Invocations);
            return Task.CompletedTask;
        });

        await fixture.DecideAsync(approve);

        Assert.Equal(status, fixture.Request.GetCurrentStatus());
        Assert.True(fixture.DmResult.Task.IsCompletedSuccessfully);
        Assert.False(await fixture.DmResult.Task);
        var post = Assert.Single(fixture.Activity.Posts);
        Assert.Contains(label, post.Title);
        Assert.Contains("#37", post.Footer?.Text);
        Assert.Contains(post.Fields, field => field.Value == "<@12345>");
        Assert.Contains(post.Fields, field => field.Value.Contains("制作班"));
        Assert.Contains(post.Fields, field => field.Value == "1,234.5 円");
        Assert.DoesNotContain(post.Fields, field =>
            field.Value.Contains("個別の購入内容") || field.Value.Contains("個別の却下理由"));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => fixture.DecideAsync(approve));
        Assert.Single(fixture.Activity.Posts);
        fixture.UnitOfWork.Verify(w => w.SaveChangesAsync(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedSaveDoesNotNotify(bool approve)
    {
        var fixture = new Fixture();
        fixture.UnitOfWork.Setup(w => w.SaveChangesAsync()).ThrowsAsync(new IOException("Save failed"));
        await Assert.ThrowsAsync<IOException>(() => fixture.DecideAsync(approve));
        Assert.Empty(fixture.Activity.Client.Invocations);
        Assert.False(fixture.DmResult.Task.IsCompleted);
    }

    [Fact]
    public async Task SlowOrUnavailableChannelDoesNotDelayTheInteractionResponse()
    {
        var fixture = new Fixture();
        var channelLookup = new TaskCompletionSource<IChannel>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Activity.Client.Setup(c => c.GetChannelAsync(RecordingBudgetChannel.ChannelId, CacheMode.AllowDownload, null))
            .Returns(channelLookup.Task);
        var completion = fixture.DecideAsync(true);
        try
        {
            Assert.False(await fixture.DmResult.Task.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.False(completion.IsCompleted);
        }
        finally
        {
            channelLookup.TrySetException(new IOException("Discord channel unavailable"));
            await completion;
        }
        Assert.Equal(RequestStatus.Approved, fixture.Request.GetCurrentStatus());
    }

    [Fact]
    public async Task RequesterLookupFailureStillReportsTheSavedDecision()
    {
        var fixture = new Fixture(failRequesterLookup: true);
        await fixture.DecideAsync(false);
        Assert.True(fixture.DmResult.Task.IsCompletedSuccessfully);
        Assert.False(await fixture.DmResult.Task);
        Assert.Contains("却下", Assert.Single(fixture.Activity.Posts).Title);
    }

    [Theory]
    [InlineData("approve", "承認", RequestStatus.Approved)]
    [InlineData("reject", "却下", RequestStatus.Rejected)]
    [InlineData("approve-button", "承認", RequestStatus.Approved)]
    [InlineData("reject-modal", "却下", RequestStatus.Rejected)]
    public async Task CommandsAndRequestDetailActionsPublishTheSavedDecision(string action, string label, RequestStatus status)
    {
        // Discord.Net creates socket users internally. Only the invoking ID is needed.
        var user = (SocketSelfUser)RuntimeHelpers.GetUninitializedObject(typeof(SocketSelfUser));
        var interaction = (SocketSlashCommand)RuntimeHelpers.GetUninitializedObject(typeof(SocketSlashCommand));
        typeof(SocketInteraction).GetProperty(nameof(SocketInteraction.User))!.SetValue(interaction, user);
        var context = new SocketInteractionContext(null!, interaction);
        var fixture = new Fixture(user.Id);

        var slash = new Mock<ApprovalModule>(null!, null!, fixture.Workflow, null!, NullLogger<ApprovalModule>.Instance).Object;
        var detail = new Mock<RequestComponentModule>(null!, null!, null!, null!, fixture.Workflow,
            NullLogger<RequestComponentModule>.Instance, null!, fixture.Activity.Service).Object;
        ((IInteractionModuleBase)slash).SetContext(context);
        ((IInteractionModuleBase)detail).SetContext(context);
        await (action switch
        {
            "approve" => slash.Approve(37),
            "reject" => slash.Reject(37, "個別の却下理由"),
            "approve-button" => detail.ApproveRequestFromButton("37"),
            "reject-modal" => detail.RejectRequestFromModal("37", new RejectReasonModal { Reason = "個別の却下理由" }),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        });

        Assert.Equal(status, fixture.Request.GetCurrentStatus());
        Assert.Contains(label, Assert.Single(fixture.Activity.Posts).Title);
        var replies = Mock.Get(slash).Invocations.Concat(Mock.Get(detail).Invocations)
            .Where(call => call.Method.Name is "RespondAsync" or "FollowupAsync");
        var reply = Assert.Single(replies);
        Assert.Contains(label, Assert.Single(reply.Arguments.OfType<Embed>()).Title);
    }

    private sealed class Fixture
    {
        public RecordingBudgetChannel Activity { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public TaskCompletionSource<bool> DmResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public BudgetRequest Request { get; }
        public RequestWorkflowInteractionService Workflow { get; }

        public Fixture(ulong accountantDiscordId = 12345, bool failRequesterLookup = false)
        {
            var group = new Group("制作班");
            typeof(Group).GetProperty(nameof(Group.Id))!.SetValue(group, 8);
            var accountant = new User("会計", accountantDiscordId, AccountRole.Accountant);
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(accountant, 7);
            var requester = new User("申請者", 54321, AccountRole.GroupLeader, group.Id);
            var year = new FiscalYear(4);
            group.AddBudgetTransaction(new BudgetTransaction(true, 5000, year));
            Request = group.CreateBudgetRequest(requester, new Money(1234.5m), year, "個別の購入内容", []);
            typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(Request, 37);
            var groups = new Mock<IGroupRepository>();
            groups.Setup(r => r.GetAllAsync()).ReturnsAsync([group]);
            groups.Setup(r => r.GetByIdAsync(8)).ReturnsAsync(group);
            var users = new Mock<IUserRepository>();
            users.Setup(r => r.GetByDiscordUserIdAsync(accountantDiscordId)).ReturnsAsync(accountant);
            users.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(accountant);
            // The requester cannot be resolved for DM delivery.
            if (failRequesterLookup)
                users.Setup(r => r.GetByIdAsync(requester.Id)).ThrowsAsync(new IOException("Lookup failed"));
            var details = new RequestDetailUseCase(groups.Object, users.Object, Mock.Of<IFileStorage>());
            var notifications = new DiscordRequestNotificationService(users.Object, null!,
                new NotifyApprovedRequestUseCase(details, users.Object), new NotifyRejectedRequestUseCase(details, users.Object));
            Workflow = new RequestWorkflowInteractionService(
                new ApproveBudgetRequestUseCase(groups.Object, users.Object, UnitOfWork.Object),
                new RejectBudgetRequestUseCase(groups.Object, users.Object, UnitOfWork.Object),
                new RequestQueryUseCase(groups.Object, users.Object), notifications, Activity.Service,
                NullLogger<RequestWorkflowInteractionService>.Instance);
        }

        public Task DecideAsync(bool approve) => approve
            ? Workflow.ApproveAsync(37, 12345, RecordDmResult)
            : Workflow.RejectAsync(37, 12345, "個別の却下理由", RecordDmResult);

        private Task RecordDmResult(bool sent)
        {
            DmResult.SetResult(sent);
            return Task.CompletedTask;
        }
    }
}
