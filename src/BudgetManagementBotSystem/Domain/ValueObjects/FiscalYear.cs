namespace BudgetManagementBotSystem.Domain.ValueObjects;

public class FiscalYear
{
    public int Year { get; private set; }
    public int StartMonth { get; private set; } = 4;

    private FiscalYear() { } // EF Core用

    public FiscalYear(int startMonth)
    {
        StartMonth = startMonth;
        Year = DateTime.Now.Year;
        if (DateTime.Now.Month < StartMonth) Year -= 1;
    }

    public FiscalYear(int year, int startMonth)
    {
        Year = year;
        StartMonth = startMonth;
    }

    public override string ToString() => Year.ToString();
}
