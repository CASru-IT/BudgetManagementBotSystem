namespace BudgetManagementBotSystem.Domain.ValueObjects;

public class Money
{
    public decimal Value { get; private set; }

    public Money(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Money value cannot be negative");
        }
        this.Value = value;
    }

    public static Money operator +(Money a, Money b)
    {
        return new Money(a.Value + b.Value);
    }
    public static Money operator -(Money a, Money b)
    {
        if (a.Value < b.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(b), "Resulting money value cannot be negative");
        }
        return new Money(a.Value - b.Value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
