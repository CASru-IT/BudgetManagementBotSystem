using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Domain.Entities;

public class User
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public ulong DiscordUserId { get; private set; }
    public int? GroupId { get; private set; }
    public AccountRole Role { get; private set; }
    public bool IsActive { get; private set; }

    public User(string name, ulong discordUserId, AccountRole role, int? groupId = null)
    {
        Name = name;
        DiscordUserId = discordUserId;
        Role = role;
        GroupId = groupId;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void ChangeRole(AccountRole role)
    {
        Role = role;
    }

    public void ChangeGroupId(int? groupId)
    {
        GroupId = groupId;
    }
}
