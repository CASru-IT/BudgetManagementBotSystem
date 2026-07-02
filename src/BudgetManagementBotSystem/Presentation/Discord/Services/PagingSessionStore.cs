using System.Collections.Concurrent;
using BudgetManagementBotSystem.Presentation.Discord.Models;

namespace BudgetManagementBotSystem.Presentation.Discord.Services;

public class PagingSessionStore
{
    private readonly ConcurrentDictionary<string, PagingSession> _sessions = new();

    public string Create(PagingSession session)
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        session.Token = token;
        session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        _sessions[token] = session;
        return token;
    }

    public bool TryGet(string token, out PagingSession? session)
    {
        CleanupExpired();
        return _sessions.TryGetValue(token, out session);
    }

    public void Update(PagingSession session)
    {
        session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        _sessions[session.Token] = session;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _sessions.Where(x => x.Value.ExpiresAt <= now))
        {
            _sessions.TryRemove(item.Key, out _);
        }
    }
}
