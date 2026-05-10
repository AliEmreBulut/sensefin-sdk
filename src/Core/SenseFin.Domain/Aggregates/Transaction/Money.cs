using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.Transaction;

/// <summary>
/// Value object representing a monetary amount with currency.
/// </summary>
public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a new Money value object.
    /// </summary>
    /// <param name="amount">Transaction amount (must be positive).</param>
    /// <param name="currency">ISO 4217 currency code (e.g., USD, EUR, TRY).</param>
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
