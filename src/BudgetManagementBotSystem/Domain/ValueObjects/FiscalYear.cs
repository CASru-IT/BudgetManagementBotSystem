namespace BudgetManagementBotSystem.Domain.ValueObjects;

public class FiscalYear
{
    public int Year { get; private set; }
    private int FiscalYearStartMonth = 4;

    public FiscalYear(int StartMonth)
    {
        FiscalYearStartMonth = StartMonth;
        Year = DateTime.Now.Year;
        if (DateTime.Now.Month < FiscalYearStartMonth) Year -= 1;
    }

    public override string ToString()
    {
        return Year.ToString();
    }
}
