namespace BudgetManagementBotSystem.InfraStructure.Discord;

public sealed record DeprecatedSlashCommandInfo(
    string CommandName,
    string? ReplacementCommandName,
    string Message);

public static class DeprecatedSlashCommands
{
    private const string DefaultMessage = "このコマンドは更新により廃止されました。新しいコマンドを使用してください。";

    private static readonly IReadOnlyDictionary<string, DeprecatedSlashCommandInfo> Commands =
        new Dictionary<string, DeprecatedSlashCommandInfo>(StringComparer.Ordinal)
        {
            ["grant-role"] = Create("grant-role", "set-user-role"),
            ["revoke-role"] = Create("revoke-role", "set-user-role")
        };

    public static bool TryGet(string commandName, out DeprecatedSlashCommandInfo info)
    {
        return Commands.TryGetValue(commandName, out info!);
    }

    private static DeprecatedSlashCommandInfo Create(string commandName, string? replacementCommandName = null)
    {
        var message = string.IsNullOrWhiteSpace(replacementCommandName)
            ? DefaultMessage
            : $"このコマンドは更新により廃止されました。代わりに `/{replacementCommandName}` を使用してください。";

        return new DeprecatedSlashCommandInfo(commandName, replacementCommandName, message);
    }
}
