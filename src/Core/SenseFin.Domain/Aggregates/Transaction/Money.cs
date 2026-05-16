using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.Transaction;

// Para miktarını ve birimini temsil eden Value Object.
public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    // Yeni bir Money nesnesi oluşturur
    public static Money Create(decimal amount, string currency)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a valid 3-letter ISO 4217 code.", nameof(currency));

        return new Money(amount, currency.ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:N2} {Currency}";
}
