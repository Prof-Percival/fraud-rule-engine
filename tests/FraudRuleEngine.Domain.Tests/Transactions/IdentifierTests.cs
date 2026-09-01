using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Transactions;

public sealed class IdentifierTests
{
    [Fact]
    public void Round_trips_a_valid_value()
    {
        CustomerId.From("CUST-4471").Value.ShouldBe("CUST-4471");
        CustomerId.From("CUST-4471").ToString().ShouldBe("CUST-4471");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Rejects_a_blank_value(string? value)
    {
        Should.Throw<ArgumentException>(() => CustomerId.From(value!));
    }

    [Fact]
    public void Rejects_a_value_beyond_the_maximum_length()
    {
        var tooLong = new string('x', CustomerId.MaximumLength + 1);

        Should.Throw<ArgumentOutOfRangeException>(() => CustomerId.From(tooLong));
    }

    [Fact]
    public void Accepts_a_value_at_exactly_the_maximum_length()
    {
        var atLimit = new string('x', CustomerId.MaximumLength);

        CustomerId.From(atLimit).Value.Length.ShouldBe(CustomerId.MaximumLength);
    }

    [Fact]
    public void Compares_by_value()
    {
        CustomerId.From("CUST-1").ShouldBe(CustomerId.From("CUST-1"));
        CustomerId.From("CUST-1").ShouldNotBe(CustomerId.From("CUST-2"));
    }

    [Fact]
    public void Is_case_sensitive()
    {
        // Deliberate. An upstream identifier is an opaque token and this service is not entitled to
        // decide that two spellings of it mean the same customer.
        CustomerId.From("cust-1").ShouldNotBe(CustomerId.From("CUST-1"));
    }

    [Fact]
    public void A_defaulted_identifier_reports_itself_as_uninitialised()
    {
        var defaulted = default(CustomerId);

        defaulted.IsInitialised.ShouldBeFalse();
        defaulted.ToString().ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => defaulted.Value);
    }

    [Fact]
    public void An_initialised_identifier_reports_itself_as_initialised()
    {
        CustomerId.From("CUST-1").IsInitialised.ShouldBeTrue();
    }

    [Fact]
    public void Different_identifier_types_are_not_interchangeable()
    {
        // This is the whole reason these types exist. The assertion below is really about the three
        // lines that will not compile, so they are spelled out rather than left implied:
        //
        //   CustomerId id = AccountId.From("ACC-1");        // does not compile
        //   AccountId id = CustomerId.From("CUST-1");       // does not compile
        //   CustomerId.From("CUST-1") == AccountId.From("CUST-1");  // does not compile
        //
        // What can be asserted at runtime is that wrapping the same string in two different types
        // produces two values that share nothing but their text.
        const string Shared = "SHARED-0001";

        CustomerId.From(Shared).Value.ShouldBe(AccountId.From(Shared).Value);
        CustomerId.From(Shared).GetType().ShouldNotBe(AccountId.From(Shared).GetType());
    }
}
