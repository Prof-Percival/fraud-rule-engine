using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Transactions;

public sealed class MoneyTests
{
    public static TheoryData<decimal> AmountsWithinScale =>
    [
        0m,
        1m,
        1.5m,
        99.99m,
        1.125m,
        1.1234m,
        -1.1234m,
        -999_999_999.9999m,
    ];

    public static TheoryData<decimal> AmountsExceedingScale =>
    [
        0.00001m,
        1.12345m,
        -1.12345m,
        99.999999m,
    ];

    [Theory]
    [MemberData(nameof(AmountsWithinScale))]
    public void Accepts_amounts_within_the_supported_scale(decimal amount)
    {
        var money = new Money(amount, Currency.Zar);

        money.Amount.ShouldBe(amount);
        money.Currency.ShouldBe(Currency.Zar);
    }

    [Theory]
    [MemberData(nameof(AmountsExceedingScale))]
    public void Rejects_amounts_carrying_more_than_four_decimal_places(decimal amount)
    {
        // Rejected rather than rounded. Quietly rounding a figure somebody supplied is how
        // reconciliation breaks in ways nobody can trace.
        var act = () => { _ = new Money(amount, Currency.Zar); };

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("amount");
    }

    [Fact]
    public void Rejects_an_amount_without_a_currency()
    {
        var act = () => { _ = new Money(100m, Currency.None); };

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("currency");
    }

    [Fact]
    public void Adds_amounts_in_the_same_currency()
    {
        var result = new Money(100.50m, Currency.Zar) + new Money(49.50m, Currency.Zar);

        result.ShouldBe(new Money(150.00m, Currency.Zar));
    }

    [Fact]
    public void Subtracts_amounts_in_the_same_currency()
    {
        var result = new Money(100.00m, Currency.Zar) - new Money(150.00m, Currency.Zar);

        result.Amount.ShouldBe(-50.00m);
        result.Currency.ShouldBe(Currency.Zar);
    }

    [Fact]
    public void Negates_an_amount()
    {
        new Money(100m, Currency.Zar).Negate().Amount.ShouldBe(-100m);
        (-new Money(100m, Currency.Zar)).Amount.ShouldBe(-100m);
    }

    [Fact]
    public void Refuses_to_add_amounts_in_different_currencies()
    {
        var rand = new Money(100m, Currency.Zar);
        var dollars = new Money(100m, Currency.Usd);

        var exception = Should.Throw<CurrencyMismatchException>(() => rand + dollars);

        exception.Left.ShouldBe(Currency.Zar);
        exception.Right.ShouldBe(Currency.Usd);
    }

    [Fact]
    public void Refuses_to_subtract_amounts_in_different_currencies()
    {
        var rand = new Money(100m, Currency.Zar);
        var euros = new Money(100m, Currency.Eur);

        Should.Throw<CurrencyMismatchException>(() => rand - euros);
    }

    [Fact]
    public void Refuses_to_compare_amounts_in_different_currencies()
    {
        // The important case. Without this, a threshold expressed in rand would silently be
        // applied to a transaction in dollars, and the rule would fire on the wrong number.
        var rand = new Money(100m, Currency.Zar);
        var dollars = new Money(100m, Currency.Usd);

        Should.Throw<CurrencyMismatchException>(() => rand > dollars);
        Should.Throw<CurrencyMismatchException>(() => rand < dollars);
        Should.Throw<CurrencyMismatchException>(() => rand >= dollars);
        Should.Throw<CurrencyMismatchException>(() => rand <= dollars);
        Should.Throw<CurrencyMismatchException>(() => rand.CompareTo(dollars));
    }

    [Fact]
    public void Compares_amounts_in_the_same_currency()
    {
        var smaller = new Money(100m, Currency.Zar);
        var larger = new Money(250m, Currency.Zar);
        var equalToSmaller = new Money(100m, Currency.Zar);

        (larger > smaller).ShouldBeTrue();
        (smaller < larger).ShouldBeTrue();
        (larger >= smaller).ShouldBeTrue();
        (smaller <= larger).ShouldBeTrue();

        // The boundary. A threshold rule asks "is the amount at or above the limit", so the
        // equal case has to be settled rather than left to chance.
        (smaller >= equalToSmaller).ShouldBeTrue();
        (smaller <= equalToSmaller).ShouldBeTrue();
        (smaller > equalToSmaller).ShouldBeFalse();
        (smaller < equalToSmaller).ShouldBeFalse();
    }

    [Fact]
    public void Sorts_by_amount_within_a_currency()
    {
        Money[] amounts =
        [
            new(300m, Currency.Zar),
            new(100m, Currency.Zar),
            new(200m, Currency.Zar),
        ];

        Array.Sort(amounts);

        amounts.Select(money => money.Amount).ShouldBe([100m, 200m, 300m]);
    }

    [Fact]
    public void Treats_amounts_of_equal_value_as_equal_regardless_of_trailing_zeroes()
    {
        // 100.00 and 100 are the same amount of money even though the decimals carry different
        // scale. Worth pinning down, because if equality were scale sensitive then a threshold
        // comparison would depend on how the number was written.
        new Money(100.00m, Currency.Zar).ShouldBe(new Money(100m, Currency.Zar));
    }

    [Fact]
    public void Distinguishes_the_same_amount_in_different_currencies()
    {
        new Money(100m, Currency.Zar).ShouldNotBe(new Money(100m, Currency.Usd));
    }

    [Fact]
    public void Reports_sign_and_zero()
    {
        var positive = new Money(0.01m, Currency.Zar);
        var negative = new Money(-0.01m, Currency.Zar);
        var zero = Money.Zero(Currency.Zar);

        positive.IsPositive.ShouldBeTrue();
        positive.IsNegative.ShouldBeFalse();
        positive.IsZero.ShouldBeFalse();

        negative.IsNegative.ShouldBeTrue();
        negative.IsPositive.ShouldBeFalse();

        zero.IsZero.ShouldBeTrue();
        zero.IsPositive.ShouldBeFalse();
        zero.IsNegative.ShouldBeFalse();
        zero.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Formats_using_invariant_culture()
    {
        // The machine this runs on is set to a locale using a comma as the decimal separator, so
        // a culture sensitive ToString would produce "1234,56 ZAR" here and "1234.56 ZAR" in CI.
        // Log output and problem responses need to be the same everywhere.
        var formatted = new Money(1234.56m, Currency.Zar).ToString();

        formatted.ShouldBe("1234.56 ZAR");
        formatted.ShouldNotContain(",");
    }

    [Fact]
    public void CompareTo_object_orders_null_first_and_rejects_other_types()
    {
        var money = new Money(100m, Currency.Zar);

        money.CompareTo(null).ShouldBe(1);
        money.CompareTo((object)new Money(50m, Currency.Zar)).ShouldBeGreaterThan(0);
        Should.Throw<ArgumentException>(() => money.CompareTo("not money"));
    }

    [Fact]
    public void A_defaulted_instance_has_no_currency_and_cannot_be_used_in_arithmetic()
    {
        // A struct always has a default and no constructor can intercept it, so this documents
        // what that default does rather than pretending it cannot happen. Arithmetic on it fails
        // because the result would have no currency.
        var defaulted = default(Money);

        defaulted.Currency.ShouldBe(Currency.None);
        defaulted.Amount.ShouldBe(0m);
        Should.Throw<ArgumentOutOfRangeException>(() => defaulted + defaulted);
    }
}
