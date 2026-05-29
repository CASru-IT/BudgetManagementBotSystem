using BudgetManagementBotSystem.Domain.Repository;
using System.Threading.Tasks;
using System.Linq;
using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers
{
    public static class AuthorizationHelper
    {
        /// <summary>
        /// 指定したロールのいずれかに該当するかを判定します。
        /// </summary>
        public static async Task<bool> IsPrivilegedAsync(IUserRepository userRepository, ulong discordUserId, params AccountRole[] allowedRoles)
        {
            if (userRepository == null) return false;

            var user = await userRepository.GetByDiscordUserIdAsync(discordUserId);
            if (user == null) return false;

            if (allowedRoles == null || allowedRoles.Length == 0) return false;

            return allowedRoles.Contains(user.Role);
        }
    }
}
