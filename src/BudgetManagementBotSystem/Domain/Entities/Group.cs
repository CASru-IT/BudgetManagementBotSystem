namespace BudgetManagementBotSystem.Domain.Entities;

public class Group
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public List<User> Users { get; private set; } = new List<User>();
    public List<BudgetTransaction> BudgetTransactions { get; private set; } = new List<BudgetTransaction>();
    public List<BudgetRequest> Requests { get; private set; } = new List<BudgetRequest>();

    public Group(string name)
    {
        Name = name;
    }
}
