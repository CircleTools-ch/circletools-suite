using AppSuite.SharedKernel;

namespace AppSuite.SecretSanta.Core.Domain;

/// <summary>Value object describing the gift budget for a group.</summary>
public sealed class Budget : ValueObject
{
    private Budget(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Gets the monetary amount.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the ISO currency code (e.g. CHF).</summary>
    public string Currency { get; }

    /// <summary>Creates a <see cref="Budget"/>.</summary>
    /// <param name="amount">The amount; must not be negative.</param>
    /// <param name="currency">The currency code; must not be empty.</param>
    /// <returns>The created budget.</returns>
    /// <exception cref="ArgumentException">Thrown when the amount is negative or the currency is empty.</exception>
    public static Budget Create(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Budget amount must not be negative.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency must not be empty.", nameof(currency));
        }

        return new Budget(amount, currency.Trim());
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
