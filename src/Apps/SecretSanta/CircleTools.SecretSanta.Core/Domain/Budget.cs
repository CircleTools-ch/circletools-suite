// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// Value object describing the gift budget for an event. <see cref="Amount"/> is the spending
/// <strong>maximum</strong> (the upper limit every giver sees). An optional <see cref="MinAmount"/>
/// adds a lower bound, so a group can ask for a gift worth <em>at least</em> a floor as well as
/// <em>at most</em> the limit (FR-036) — both are expressed in the single <see cref="Currency"/>.
/// </summary>
public sealed class Budget : ValueObject
{
    private Budget(decimal amount, string currency, decimal? minAmount)
    {
        Amount = amount;
        Currency = currency;
        MinAmount = minAmount;
    }

    /// <summary>Gets the maximum (upper-limit) amount every giver is shown.</summary>
    public decimal Amount { get; }

    /// <summary>
    /// Gets the optional minimum (lower-bound) amount, or <see langword="null"/> when the budget is a
    /// bare maximum (today's behaviour). When set it is at least zero and never exceeds <see cref="Amount"/>.
    /// </summary>
    public decimal? MinAmount { get; }

    /// <summary>Gets the ISO currency code (e.g. CHF).</summary>
    public string Currency { get; }

    /// <summary>Creates a <see cref="Budget"/>.</summary>
    /// <param name="amount">The maximum amount; must not be negative.</param>
    /// <param name="currency">The currency code; must not be empty.</param>
    /// <param name="minAmount">
    /// The optional minimum amount. When supplied it must be at least zero and must not exceed
    /// <paramref name="amount"/>.
    /// </param>
    /// <returns>The created budget.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the amount is negative, the currency is empty, or the minimum is negative or greater
    /// than the maximum.
    /// </exception>
    public static Budget Create(decimal amount, string currency, decimal? minAmount = null)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Budget amount must not be negative.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency must not be empty.", nameof(currency));
        }

        if (minAmount is { } min)
        {
            if (min < 0)
            {
                throw new ArgumentException("Budget minimum must not be negative.", nameof(minAmount));
            }

            if (min > amount)
            {
                throw new ArgumentException("Budget minimum must not exceed the maximum.", nameof(minAmount));
            }
        }

        return new Budget(amount, currency.Trim(), minAmount);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return MinAmount;
        yield return Currency;
    }
}
