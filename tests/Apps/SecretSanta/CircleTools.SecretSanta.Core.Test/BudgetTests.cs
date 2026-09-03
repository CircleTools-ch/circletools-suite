using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// Domain tests for the <see cref="Budget"/> value object, in particular the optional minimum (lower
/// bound) added by FR-036: it must be non-negative, must not exceed the maximum, and participates in
/// value equality.
/// </summary>
public sealed class BudgetTests
{
    [Fact]
    public void Create_WithoutMinimum_IsABareMaximum()
    {
        var budget = Budget.Create(30m, "CHF");

        Assert.Equal(30m, budget.Amount);
        Assert.Null(budget.MinAmount);
        Assert.Equal("CHF", budget.Currency);
    }

    [Fact]
    public void Create_WithMinimum_KeepsBothBounds()
    {
        var budget = Budget.Create(30m, "CHF", 10m);

        Assert.Equal(30m, budget.Amount);
        Assert.Equal(10m, budget.MinAmount);
    }

    [Fact]
    public void Create_AllowsMinimumEqualToMaximum()
    {
        var budget = Budget.Create(20m, "CHF", 20m);

        Assert.Equal(20m, budget.MinAmount);
    }

    [Fact]
    public void Create_RejectsMinimumGreaterThanMaximum() =>
        Assert.Throws<ArgumentException>(() => Budget.Create(20m, "CHF", 30m));

    [Fact]
    public void Create_RejectsNegativeMinimum() =>
        Assert.Throws<ArgumentException>(() => Budget.Create(30m, "CHF", -1m));

    [Fact]
    public void Equality_DistinguishesTheMinimum()
    {
        var maxOnly = Budget.Create(30m, "CHF");
        var withMin = Budget.Create(30m, "CHF", 10m);

        Assert.NotEqual(maxOnly, withMin);
        Assert.Equal(Budget.Create(30m, "CHF", 10m), withMin);
    }
}
