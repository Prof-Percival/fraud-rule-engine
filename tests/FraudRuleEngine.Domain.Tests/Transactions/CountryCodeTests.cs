using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Transactions;

public sealed class CountryCodeTests
{
    [Theory]
    [InlineData("ZA")]
    [InlineData("US")]
    [InlineData("GB")]
    public void Accepts_a_two_letter_code(string value)
    {
        CountryCode.From(value).Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("za", "ZA")]
    [InlineData("zA", "ZA")]
    [InlineData("Us", "US")]
    public void Normalises_case(string input, string expected)
    {
        // A country code is a case insensitive identifier, so normalising is canonicalising rather
        // than altering meaning. Without it, "za" and "ZA" would be two different countries as far
        // as the impossible travel rule is concerned.
        CountryCode.From(input).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("ZAF")]
    [InlineData("Z1")]
    [InlineData("12")]
    [InlineData("Z ")]
    public void Rejects_anything_that_is_not_two_letters(string value)
    {
        Should.Throw<ArgumentException>(() => CountryCode.From(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Rejects_a_blank_value(string? value)
    {
        Should.Throw<ArgumentException>(() => CountryCode.From(value!));
    }

    [Fact]
    public void Accepts_a_well_formed_code_that_is_not_actually_assigned()
    {
        // Format is validated, membership of the real ISO register is not. Shipping a copy of that
        // list means maintaining it, and an unassigned code costs nothing: the impossible travel
        // rule finds no coordinates for it and declines to fire.
        CountryCode.From("XX").Value.ShouldBe("XX");
    }

    [Fact]
    public void Compares_by_value_after_normalisation()
    {
        CountryCode.From("za").ShouldBe(CountryCode.From("ZA"));
        CountryCode.From("ZA").ShouldNotBe(CountryCode.From("US"));
    }

    [Fact]
    public void A_defaulted_code_reports_itself_as_uninitialised()
    {
        var defaulted = default(CountryCode);

        defaulted.IsInitialised.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => defaulted.Value);
    }
}
