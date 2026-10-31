using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Domain.Entities;

public class User
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ulong DiscordUserId { get; private set; }
    private readonly List<UserGroupMembership> _memberships = new();
    public IReadOnlyCollection<UserGroupMembership> Memberships => _memberships.AsReadOnly();
    public IReadOnlyList<int> GroupIds => _memberships.Select(m => m.GroupId).OrderBy(id => id).ToArray();
    // Retained only for compatibility with the previous database/application version.
    internal int? LegacyGroupId { get; private set; }
    public AccountRole Role { get; private set; }
    public bool IsActive { get; private set; }

    private User() { }

    public User(string name, ulong discordUserId, AccountRole role, int? groupId = null)
    {
        Name = name;
        DiscordUserId = discordUserId;
        Role = role;
        if (groupId.HasValue) AddGroup(groupId.Value);
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

    public void ChangeName(string name)
    {
        Name = name;
    }

    public void ChangeRole(AccountRole role)
    {
        Role = role;
    }

    public bool IsMemberOf(int groupId) => _memberships.Any(m => m.GroupId == groupId);

    public void AddGroup(int groupId)
    {
        if (IsMemberOf(groupId)) return;
        _memberships.Add(new UserGroupMembership(groupId));
        LegacyGroupId ??= groupId;
    }

    public void RemoveGroup(int groupId)
    {
        _memberships.RemoveAll(m => m.GroupId == groupId);
        if (LegacyGroupId == groupId)
            LegacyGroupId = GroupIds.Select(id => (int?)id).FirstOrDefault();
    }
}
