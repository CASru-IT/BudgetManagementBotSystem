namespace BudgetManagementBotSystem.Domain.ValueObjects;

public class Money
{
    public decimal Value { get; private set; }

    public Money(decimal Value)
    {
        this.Value = Value;
    }

    public static Money operator +(Money a, Money b)
    {
        return new Money(a.Value + b.Value);
    }
    public static Money operator -(Money a, Money b)
    {
        if (a.Value < b.Value)
        {
            throw new InvalidOperationException("Resulting Value cannot be negative.");
        }
        return new Money(a.Value - b.Value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
