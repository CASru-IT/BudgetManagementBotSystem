namespace BudgetManagementBotSystem.Domain.Entities;

public class RequestEvidence
{
    public int Id { get; private set; }
    public string FilePath { get; private set; }

    public RequestEvidence(string filePath)
    {
        FilePath = filePath;
    }
}
