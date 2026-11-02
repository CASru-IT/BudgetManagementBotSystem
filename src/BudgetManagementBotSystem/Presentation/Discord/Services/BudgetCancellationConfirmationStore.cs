using System.Collections.Concurrent;
using BudgetManagementBotSystem.Application.UseCases.Budget;

namespace BudgetManagementBotSystem.Presentation.Discord.Services;

public sealed record BudgetCancellationConfirmation(ulong OwnerDiscordUserId, BudgetCancellationPreview Preview, DateTimeOffset ExpiresAt);

public class BudgetCancellationConfirmationStore(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, BudgetCancellationConfirmation> _items = new();

    public string Create(ulong owner, BudgetCancellationPreview preview)
    {
        foreach (var entry in _items.Where(x => x.Value.ExpiresAt <= _clock.GetUtcNow()))
            _items.TryRemove(entry);
        var token = Guid.NewGuid().ToString("N");
        _items[token] = new(owner, preview, _clock.GetUtcNow().AddMinutes(10));
        return token;
    }

    public bool TryTake(string token, ulong owner, out BudgetCancellationConfirmation? confirmation)
    {
        confirmation = null;
        if (!_items.TryGetValue(token, out var item) || item.OwnerDiscordUserId != owner) return false;
        if (!_items.TryRemove(new KeyValuePair<string, BudgetCancellationConfirmation>(token, item))) return false;
        if (item.ExpiresAt <= _clock.GetUtcNow()) return false;
        confirmation = item;
        return true;
    }
}
