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

    public static bool operator ==(FiscalYear a, FiscalYear b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Year == b.Year && a.StartMonth == b.StartMonth;
    }

    public static bool operator !=(FiscalYear a, FiscalYear b)
    {
        return !(a == b);
    }

    public override bool Equals(object? obj)
    {
        if (obj is FiscalYear other)
        {
            return this == other;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Year, StartMonth);
    }

    public override string ToString() => Year.ToString();
}
