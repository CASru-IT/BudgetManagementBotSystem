using BudgetManagementBotSystem.Domain.Repository;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetManagementBotSystem.Presentation.Discord.Autocomplete;

public class GroupAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var groupRepository = services.GetRequiredService<IGroupRepository>();
        var input = AutocompleteText.CurrentValue(interaction);
        var groups = await groupRepository.GetAllAsync() ?? new();

        var suggestions = groups
            .Where(group => string.IsNullOrWhiteSpace(input)
                || AutocompleteText.MatchesId(group.Id, input)
                || AutocompleteText.Contains(group.Name, input))
            .OrderBy(group => group.Id)
            .Take(AutocompleteText.MaxSuggestions)
            .Select(group => new AutocompleteResult(
                AutocompleteText.TruncateName($"{group.Name} (Group ID: {group.Id})"),
                AutocompleteText.ValueForParameter(group.Id, parameter)));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
