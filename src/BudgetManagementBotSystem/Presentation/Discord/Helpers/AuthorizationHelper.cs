using Discord.WebSocket;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers
{
    public static class AuthorizationHelper
    {
        public static bool IsOfficerOrAdmin(SocketUser user)
        {
            // TODO: 実際のロール判定を実装する
            return false;
        }
    }
}
