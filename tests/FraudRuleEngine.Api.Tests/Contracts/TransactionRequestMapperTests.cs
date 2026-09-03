using FraudRuleEngine.Api.Contracts;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Api.Tests.Contracts;

public sealed class TransactionRequestMapperTests
{
    [Fact]
    public void Maps_a_complete_request()
    {
        var mapped = TransactionRequestMapper.TryMap(AValidRequest(), out var transaction, out var errors);

        mapped.ShouldBeTrue();
        errors.ShouldBeEmpty();

        transaction.EventId.Value.ShouldBe("evt-1");
        transaction.CustomerId.Value.ShouldBe("CUST-4471");
        transaction.Amount.ShouldBe(new Money(48_500.00m, Currency.Zar));
        transaction.Category.ShouldBe(TransactionCategory.CashWithdrawal);
        transaction.Channel.ShouldBe(TransactionChannel.Atm);
        transaction.Country.Value.ShouldBe("ZA");
    }

    [Fact]
    public void Keeps_the_offset_the_caller_sent()
    {
        // The offset is the only record of the local wall clock, which the unusual hour rule reads.
        var request = AValidRequest() with
        {
            OccurredAt = new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.FromHours(2)),
        };

        TransactionRequestMapper.TryMap(request, out var transaction, out _).ShouldBeTrue();

        transaction.OccurredAt.Offset.ShouldBe(TimeSpan.FromHours(2));
        transaction.LocalTimeOfDay.ShouldBe(new TimeOnly(3, 0));
    }

    [Theory]
    [InlineData("ZAR", Currency.Zar)]
    [InlineData("zar", Currency.Zar)]
    [InlineData("Usd", Currency.Usd)]
    public void Accepts_a_currency_in_any_case(string currency, Currency expected)
    {
        var request = AValidRequest() with { Currency = currency };

        TransactionRequestMapper.TryMap(request, out var transaction, out _).ShouldBeTrue();

        transaction.Amount.Currency.ShouldBe(expected);
    }

    [Fact]
    public void Normalises_a_lower_case_country_code()
    {
        var request = AValidRequest() with { CountryCode = "za" };

        TransactionRequestMapper.TryMap(request, out var transaction, out _).ShouldBeTrue();

        transaction.Country.Value.ShouldBe("ZA");
    }

    [Fact]
    public void Falls_back_to_unknown_for_a_category_or_channel_it_does_not_recognise()
    {
        // The upstream owns this vocabulary and can add a value at any time. Refusing traffic over a word
        // this build has not seen would turn an upstream release into an outage, and the rules already
        // treat Unknown as carrying no signal.
        var request = AValidRequest() with { Category = "PetGrooming", Channel = "Telepathy" };

        var mapped = TransactionRequestMapper.TryMap(request, out var transaction, out var errors);

        mapped.ShouldBeTrue();
        errors.ShouldBeEmpty();
        transaction.Category.ShouldBe(TransactionCategory.Unknown);
        transaction.Channel.ShouldBe(TransactionChannel.Unknown);
    }

    [Fact]
    public void Rejects_a_currency_it_does_not_recognise()
    {
        // Unlike category and channel, there is no sensible default for money. Guessing would mean
        // comparing an amount against a threshold in the wrong currency.
        var request = AValidRequest() with { Currency = "XYZ" };

        TransactionRequestMapper.TryMap(request, out _, out var errors).ShouldBeFalse();

        errors.ShouldContainKey("Currency");
        errors["Currency"][0].ShouldContain("Zar");
    }

    [Fact]
    public void Reports_every_problem_rather_than_stopping_at_the_first()
    {
        // A caller fixing a payload one field per round trip is a poor experience, and worse for a batch.
        var empty = new EvaluateTransactionRequest();

        TransactionRequestMapper.TryMap(empty, out _, out var errors).ShouldBeFalse();

        errors.Keys.ShouldBe(
            [
                "EventId", "TransactionId", "CustomerId", "AccountId", "MerchantId",
                "Currency", "Category", "Channel", "Amount", "MerchantName",
                "CountryCode", "OccurredAt",
            ],
            ignoreOrder: true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-48_500)]
    public void Rejects_an_amount_that_is_not_positive(decimal amount)
    {
        var request = AValidRequest() with { Amount = amount };

        TransactionRequestMapper.TryMap(request, out _, out var errors).ShouldBeFalse();

        errors.ShouldContainKey("Amount");
    }

    [Fact]
    public void Rejects_an_amount_with_too_many_decimal_places()
    {
        // Caught here so the caller gets a named field rather than the domain throwing further in.
        var request = AValidRequest() with { Amount = 1.123456m };

        TransactionRequestMapper.TryMap(request, out _, out var errors).ShouldBeFalse();

        errors["Amount"][0].ShouldContain("4 decimal places");
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("ZAF")]
    [InlineData("Z1")]
    public void Rejects_a_malformed_country_code(string country)
    {
        var request = AValidRequest() with { CountryCode = country };

        TransactionRequestMapper.TryMap(request, out _, out var errors).ShouldBeFalse();

        errors.ShouldContainKey("CountryCode");
    }

    [Fact]
    public void Rejects_an_identifier_that_is_too_long()
    {
        var request = AValidRequest() with { CustomerId = new string('x', 65) };

        TransactionRequestMapper.TryMap(request, out _, out var errors).ShouldBeFalse();

        errors["CustomerId"][0].ShouldContain("64");
    }

    [Fact]
    public void Rejects_a_merchant_name_that_is_too_long()
    {
        var request = AValidRequest() with { MerchantName = new string('x', 201) };

        TransactionRequestMapper.TryMap(request, out _, out var errors).ShouldBeFalse();

        errors.ShouldContainKey("MerchantName");
    }

    [Fact]
    public void Rejects_a_missing_time()
    {
        var request = AValidRequest() with { OccurredAt = null };

        TransactionRequestMapper.TryMap(request, out _, out var errors).ShouldBeFalse();

        errors.ShouldContainKey("OccurredAt");
    }

    [Fact]
    public void Rejects_a_null_request()
    {
        Should.Throw<ArgumentNullException>(
            () => TransactionRequestMapper.TryMap(null!, out _, out _));
    }

    private static EvaluateTransactionRequest AValidRequest() => new()
    {
        EventId = "evt-1",
        TransactionId = "TXN-000123",
        CustomerId = "CUST-4471",
        AccountId = "ACC-9920",
        Amount = 48_500.00m,
        Currency = "ZAR",
        Category = "CashWithdrawal",
        Channel = "Atm",
        MerchantId = "MERCH-771",
        MerchantName = "ATM Sandton City",
        CountryCode = "ZA",
        OccurredAt = new DateTimeOffset(2026, 9, 1, 2, 14, 0, TimeSpan.Zero),
    };
}
