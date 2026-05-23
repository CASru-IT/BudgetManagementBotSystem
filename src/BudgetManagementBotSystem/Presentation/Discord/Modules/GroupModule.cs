using Discord.Interactions;
using BudgetManagementBotSystem.Application.UseCases;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules
{
    public class GroupModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly RegisterGroupUseCase _registerGroupUseCase;

        public GroupModule(RegisterGroupUseCase registerGroupUseCase)
        {
            _registerGroupUseCase = registerGroupUseCase;
        }

        [SlashCommand("register-group", "新しい班を登録する")]
        public async Task RegisterGroup([Summary("group-name")] string name)
        {
            await _registerGroupUseCase.ExecuteAsync(name);
            await RespondAsync($"班を登録しました: {name}");
        }

        [SlashCommand("delete-group", "班を削除または無効化する")]
        public async Task DeleteGroup() => await RespondAsync("未実装: 班削除");
    }
}
