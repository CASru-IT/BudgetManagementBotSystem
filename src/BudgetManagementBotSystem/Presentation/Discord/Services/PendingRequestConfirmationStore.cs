using System.Collections.Concurrent;
using BudgetManagementBotSystem.Presentation.Discord.Models;

namespace BudgetManagementBotSystem.Presentation.Discord.Services;

public class PendingRequestConfirmationStore
{
    private readonly ConcurrentDictionary<string, PendingRequestConfirmation> _items = new();

    public string Create(PendingRequestConfirmation confirmation)
    {
        var token = Guid.NewGuid().ToString("N");
        confirmation.Token = token;
        confirmation.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);

        _items[token] = confirmation;
        return token;
    }

    public bool TryGet(string token, out PendingRequestConfirmation? confirmation)
    {
        CleanupExpired();
        return _items.TryGetValue(token, out confirmation);
    }

    public bool TryRemove(string token, out PendingRequestConfirmation? confirmation)
    {
        CleanupExpired();
        return _items.TryRemove(token, out confirmation);
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _items.Where(x => x.Value.ExpiresAt <= now))
        {
            _items.TryRemove(item.Key, out _);
        }
    }
}
