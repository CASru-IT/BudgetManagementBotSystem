using BudgetManagementBotSystem.Domain.Repository;
using System.Threading.Tasks;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers
{
    public static class AuthorizationHelper
    {
        /// <summary>
        /// DB 側の User.Role を参照してオフィサーまたは管理者かを判定します。
        /// Presentation 層からは IUserRepository を注入して呼び出してください。
        /// </summary>
        public static async Task<bool> IsPrivilegedAsync(IUserRepository userRepository, ulong discordUserId)
        {
            if (userRepository == null) return false;

            var user = await userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) return false;

            var role = user.Role;
            return role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Admin
                || role == BudgetManagementBotSystem.Domain.Enums.AccountRole.Accountant
                || role == BudgetManagementBotSystem.Domain.Enums.AccountRole.President
                || role == BudgetManagementBotSystem.Domain.Enums.AccountRole.GroupLeader;
        }
    }
}
