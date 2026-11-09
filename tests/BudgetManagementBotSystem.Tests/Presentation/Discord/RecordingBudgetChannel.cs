using BudgetManagementBotSystem.Presentation.Discord.Services;
using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

internal sealed class RecordingBudgetChannel
{
    public const ulong ChannelId = 812345678901234567;
    public Mock<IDiscordClient> Client { get; } = new();
    public Mock<ITextChannel> Channel { get; } = new();
    public BudgetActivityChannel Service { get; }

    public RecordingBudgetChannel() : this(ChannelId.ToString()) { }

    public RecordingBudgetChannel(string? setting)
    {
        Client.Setup(c => c.GetChannelAsync(ChannelId, CacheMode.AllowDownload, null))
            .ReturnsAsync(Channel.Object);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Discord:BudgetActivityChannelId"] = setting
        }).Build();
        Service = new BudgetActivityChannel(Client.Object, configuration, NullLogger<BudgetActivityChannel>.Instance);
    }

    public IReadOnlyList<Embed> Posts => Channel.Invocations
        .Where(call => call.Method.Name == nameof(IMessageChannel.SendMessageAsync))
        .Select(call =>
        {
            var mentions = Assert.IsType<AllowedMentions>(call.Arguments[4]);
            Assert.Equal(AllowedMentionTypes.None, mentions.AllowedTypes.GetValueOrDefault());
            Assert.Empty(mentions.UserIds);
            Assert.Empty(mentions.RoleIds);
            return Assert.IsType<Embed>(call.Arguments[2]);
        }).ToList();
}
