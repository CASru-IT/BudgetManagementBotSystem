namespace BudgetManagementBotSystem.Domain.Exceptions;

// Only these expected business errors may be shown verbatim in Discord.
public sealed class BudgetCancellationValidationException(string message, string? parameter = null)
    : ArgumentException(message, parameter);

public sealed class BudgetCancellationRejectedException(string message)
    : InvalidOperationException(message);
