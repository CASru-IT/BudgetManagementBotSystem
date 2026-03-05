namespace BudgetManagementBotSystem.Domain.ValueObjects;

public class Money
{
    public decimal Amount { get; private set; }

    public Money(decimal amount)
    {
        Amount = amount;
    }

    public static Money operator +(Money a, Money b)
    {
        return new Money(a.Amount + b.Amount);
    }
    public static Money operator -(Money a, Money b)
    {
        if (a.Amount < b.Amount)
        {
            throw new InvalidOperationException("Resulting amount cannot be negative.");
        }
        return new Money(a.Amount - b.Amount);
    }

    public override string ToString()
    {
        return Amount.ToString();
    }
}
