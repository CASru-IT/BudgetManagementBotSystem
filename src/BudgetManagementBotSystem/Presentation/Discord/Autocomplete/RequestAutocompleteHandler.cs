using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetManagementBotSystem.Presentation.Discord.Autocomplete;

public class RequestAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var requestListUseCase = services.GetRequiredService<RequestListUseCase>();
        var input = AutocompleteText.CurrentValue(interaction);

        PagedResult<PendingRequestDto> result;
        try
        {
            result = await requestListUseCase.ExecuteAsync(context.User.Id, status: null, page: 1, pageSize: 50);
        }
        catch (ArgumentException)
        {
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());
        }

        var suggestions = result.Items
            .Where(request => string.IsNullOrWhiteSpace(input)
                || AutocompleteText.MatchesId(request.Id, input)
                || AutocompleteText.Contains(request.GroupName, input)
                || AutocompleteText.Contains(request.Description, input)
                || AutocompleteText.Contains(request.Status.ToString(), input))
            .OrderByDescending(request => request.RequestDate)
            .Take(AutocompleteText.MaxSuggestions)
            .Select(request => new AutocompleteResult(
                AutocompleteText.TruncateName(
                    $"#{request.Id} {request.GroupName} {request.Amount:N0} JPY [{request.Status}] {request.Description}"),
                AutocompleteText.ValueForParameter(request.Id, parameter)));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
