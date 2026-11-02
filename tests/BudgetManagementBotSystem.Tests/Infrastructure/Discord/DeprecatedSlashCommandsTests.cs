using BudgetManagementBotSystem.InfraStructure.Discord;

namespace BudgetManagementBotSystem.Tests.Infrastructure.Discord;

public class DeprecatedSlashCommandsTests
{
    [Theory]
    [InlineData("usage-history", "budget-history")]
    [InlineData("予算履歴", "budget-history")]
    [InlineData("予算取消", "cancel-budget")]
    public void RenamedBudgetCommandsDirectToSingleEnglishReplacement(string oldName, string replacement)
    {
        Assert.True(DeprecatedSlashCommands.TryGet(oldName, out var info));
        Assert.Equal(replacement, info.ReplacementCommandName);
        Assert.Contains("/" + replacement, info.Message);
        Assert.False(DeprecatedSlashCommands.TryGet(replacement, out _));
    }

    [Theory]
    [InlineData("grant-role")]
    [InlineData("revoke-role")]
    public void TryGet_ReturnsReplacementMessage_ForDeprecatedRoleCommands(string commandName)
    {
        var found = DeprecatedSlashCommands.TryGet(commandName, out var info);

        Assert.True(found);
        Assert.Equal(commandName, info.CommandName);
        Assert.Equal("set-user-role", info.ReplacementCommandName);
        Assert.Contains("/set-user-role", info.Message);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForCurrentCommand()
    {
        var found = DeprecatedSlashCommands.TryGet("set-user-role", out _);

        Assert.False(found);
    }
}
