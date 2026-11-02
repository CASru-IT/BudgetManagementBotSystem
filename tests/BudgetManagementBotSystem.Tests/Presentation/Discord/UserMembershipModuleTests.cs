using System.Runtime.CompilerServices;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Presentation.Discord.Modules;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class UserMembershipModuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public async Task Unassign_RequiresTargetAndPreservesOtherMembership(int? groupId)
    {
        var fixture = new Fixture(AccountRole.Admin);
        await fixture.Module.UnassignGroup(1, groupId);
        Assert.Equal(groupId.HasValue ? new[] { 11 } : new[] { 10, 11 }, fixture.Target.GroupIds);
        Assert.Contains(groupId.HasValue ? "他の所属は残っています" : "group-id", fixture.Module.Response!.Description);
        fixture.Work.Verify(w => w.SaveChangesAsync(), groupId.HasValue ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(AccountRole.GroupLeader)]
    [InlineData(AccountRole.Accountant)]
    [InlineData(AccountRole.President)]
    public async Task MembershipAdministration_RemainsAdminOnly(AccountRole role)
    {
        var fixture = new Fixture(role);
        await fixture.Module.AssignGroup(1, 12);
        await fixture.Module.UnassignGroup(1, 10);
        Assert.Equal(new[] { 10, 11 }, fixture.Target.GroupIds);
        fixture.Work.Verify(w => w.SaveChangesAsync(), Times.Never());
    }

    [Fact]
    public async Task GroupMembers_AllowsSecondMembership()
    {
        var fixture = new Fixture(AccountRole.GroupLeader);
        await fixture.Module.GroupMembers(11);
        Assert.Contains("班メンバー一覧", fixture.Module.Response!.Title);
    }

    private sealed class Fixture
    {
        public User Target { get; } = new("兼任", 0, AccountRole.GroupLeader, 10);
        public Mock<IUnitOfWork> Work { get; } = new();
        public RecordingModule Module { get; }
        public Fixture(AccountRole role)
        {
            Target.ChangeRole(role);
            Target.AddGroup(11);
            var users = new Mock<IUserRepository>();
            users.Setup(r => r.GetByDiscordUserIdAsync(0)).ReturnsAsync(Target);
            users.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(Target);
            users.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User> { Target });
            Module = new RecordingModule(new UserQueryUseCase(users.Object),
                new UserCommandUseCase(users.Object, Mock.Of<IGroupRepository>(), Work.Object));
        }
    }

    private sealed class RecordingModule : UserManagementModule
    {
        public Embed? Response { get; private set; }
        public RecordingModule(UserQueryUseCase query, UserCommandUseCase command)
            : base(null!, query, command, NullLogger<UserManagementModule>.Instance)
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
        { Assert.True(ephemeral); Response = embed; return Task.CompletedTask; }
    }
}
