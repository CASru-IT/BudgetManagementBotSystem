using BudgetManagementBotSystem.Domain.Repository;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetManagementBotSystem.Presentation.Discord.Autocomplete;

public class UserAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var userRepository = services.GetRequiredService<IUserRepository>();
        var input = AutocompleteText.CurrentValue(interaction);
        var users = await userRepository.GetAllAsync() ?? new();

        var suggestions = users
            .Where(user => string.IsNullOrWhiteSpace(input)
                || AutocompleteText.MatchesId(user.Id, input)
                || user.DiscordUserId.ToString().StartsWith(input, StringComparison.OrdinalIgnoreCase)
                || AutocompleteText.Contains(user.Name, input)
                || AutocompleteText.Contains(user.Role.ToString(), input))
            .OrderBy(user => user.Id)
            .Take(AutocompleteText.MaxSuggestions)
            .Select(user =>
            {
                var state = user.IsActive ? "Active" : "Inactive";
                var group = user.GroupId.HasValue ? $", Group: {user.GroupId.Value}" : ", No group";
                return new AutocompleteResult(
                    AutocompleteText.TruncateName($"{user.Name} (User ID: {user.Id}, {user.Role}, {state}{group})"),
                    AutocompleteText.ValueForParameter(user.Id, parameter));
            });

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
