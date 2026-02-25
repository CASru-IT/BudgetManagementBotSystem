using Discord.Interactions;

namespace BudgetManagementBotSystem.Commands;

public class TestModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("test", "A test command to check if the bot is working.")]
    public async Task TestCommand()
    {
        await RespondAsync("The bot is working!");
    }
}
