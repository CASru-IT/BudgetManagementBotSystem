using BudgetManagementBotSystem.Application.UseCases.Budget;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetManagementBotSystem.Presentation.Discord.Autocomplete;

public class BudgetTransactionAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        var useCase = services.GetRequiredService<CancelBudgetAdditionUseCase>();
        IReadOnlyList<BudgetAdditionCandidate> candidates;
        try
        {
            candidates = await useCase.GetCandidatesAsync(context.User.Id, AutocompleteText.CurrentValue(interaction));
        }
        catch (UnauthorizedAccessException)
        {
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());
        }

        return AutocompletionResult.FromSuccess(candidates.Select(candidate => new AutocompleteResult(
            AutocompleteText.TruncateName(
                $"#{candidate.TransactionId} {candidate.TransactionDate.AddHours(9):yyyy-MM-dd HH:mm} {candidate.Amount:N2} JPY {candidate.FiscalYear}年度 {(candidate.IsLegacy ? "[由来未確認] " : "")}{candidate.GroupName}"),
            AutocompleteText.ValueForParameter(candidate.TransactionId, parameter))));
    }
}
