using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Transactions;

public sealed class TransactionEventTests
{
    [Fact]
    public void Carries_the_values_it_was_built_with()
    {
        var occurredAt = new DateTimeOffset(2026, 9, 1, 2, 14, 0, TimeSpan.Zero);

        var transaction = TransactionEventBuilder.AValidEvent()
            .WithCustomerId(CustomerId.From("CUST-4471"))
            .WithAmount(48_500.00m, Currency.Zar)
            .WithCategory(TransactionCategory.CashWithdrawal)
            .WithChannel(TransactionChannel.Atm)
            .WithCountry("ZA")
            .OccurringAt(occurredAt)
            .Build();

        transaction.CustomerId.Value.ShouldBe("CUST-4471");
        transaction.Amount.ShouldBe(new Money(48_500.00m, Currency.Zar));
        transaction.Category.ShouldBe(TransactionCategory.CashWithdrawal);
        transaction.Channel.ShouldBe(TransactionChannel.Atm);
        transaction.Country.Value.ShouldBe("ZA");
        transaction.OccurredAt.ShouldBe(occurredAt);
    }

    [Fact]
    public void Keeps_the_offset_the_producer_reported()
    {
        // The offset is the only record of what the wall clock read where the transaction happened,
        // so it is kept rather than folded into UTC. UTC is still available for comparison.
        var johannesburgLocal = new DateTimeOffset(2026, 9, 1, 14, 30, 0, TimeSpan.FromHours(2));

        var transaction = TransactionEventBuilder.AValidEvent()
            .OccurringAt(johannesburgLocal)
            .Build();

        transaction.OccurredAt.Offset.ShouldBe(TimeSpan.FromHours(2));
        transaction.OccurredAt.Hour.ShouldBe(14);
        transaction.OccurredAtUtc.Hour.ShouldBe(12);
        transaction.LocalTimeOfDay.ShouldBe(new TimeOnly(14, 30));
    }

    [Fact]
    public void Orders_and_subtracts_correctly_across_different_offsets()
    {
        // Why keeping the offset costs nothing elsewhere. These two are thirty minutes apart as
        // instants despite the wall clocks reading two hours apart, and DateTimeOffset knows that
        // without any normalisation on our part.
        var johannesburg = TransactionEventBuilder.AValidEvent()
            .OccurringAt(new DateTimeOffset(2026, 9, 1, 14, 0, 0, TimeSpan.FromHours(2)))
            .Build();

        var london = TransactionEventBuilder.AValidEvent()
            .OccurringAt(new DateTimeOffset(2026, 9, 1, 12, 30, 0, TimeSpan.FromHours(0)))
            .Build();

        (london.OccurredAt - johannesburg.OccurredAt).ShouldBe(TimeSpan.FromMinutes(30));
        (london.OccurredAt > johannesburg.OccurredAt).ShouldBeTrue();
    }

    [Fact]
    public void Rejects_an_amount_of_zero()
    {
        var act = Building(builder => builder.WithAmount(0m));

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("amount");
    }

    [Fact]
    public void Rejects_a_negative_amount()
    {
        // Money itself allows negatives, because subtracting two amounts is legitimate. A
        // transaction event is narrower: it models spend, and a credit would be its own event type
        // with its own rules.
        var act = Building(builder => builder.WithAmount(-100m));

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("amount");
    }

    [Fact]
    public void Rejects_a_missing_time()
    {
        var act = Building(builder => builder.OccurringAt(default));

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("occurredAt");
    }

    [Fact]
    public void Rejects_a_null_merchant()
    {
        var act = Building(builder => builder.WithMerchant(null!));

        act.ShouldThrow<ArgumentNullException>()
            .ParamName.ShouldBe("merchant");
    }

    [Fact]
    public void Rejects_a_defaulted_event_id()
    {
        // Fails here rather than at the database. A defaulted identifier that survives construction
        // surfaces as a constraint violation or a null column much later, at which point the stack
        // trace points nowhere useful.
        var act = Building(builder => builder.WithEventId(default));

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("eventId");
    }

    [Fact]
    public void Rejects_a_defaulted_transaction_id()
    {
        var act = Building(builder => builder.WithTransactionId(default));

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("transactionId");
    }

    [Fact]
    public void Rejects_a_defaulted_customer_id()
    {
        var act = Building(builder => builder.WithCustomerId(default));

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("customerId");
    }

    [Fact]
    public void Rejects_a_defaulted_account_id()
    {
        var act = Building(builder => builder.WithAccountId(default));

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("accountId");
    }

    [Fact]
    public void Rejects_a_defaulted_country()
    {
        var act = Building(builder => builder.WithCountry(default(CountryCode)));

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("country");
    }

    [Fact]
    public void Accepts_a_category_and_channel_it_does_not_recognise()
    {
        // The categorisation is owned by an upstream service. If it starts sending something new,
        // this engine has to keep working rather than reject the traffic, so Unknown is a valid
        // state that rules treat as carrying no signal.
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithCategory(TransactionCategory.Unknown)
            .WithChannel(TransactionChannel.Unknown)
            .Build();

        transaction.Category.ShouldBe(TransactionCategory.Unknown);
        transaction.Channel.ShouldBe(TransactionChannel.Unknown);
    }

    /// <summary>
    /// Applies one change to an otherwise valid event and returns the attempt to build it.
    /// </summary>
    /// <remarks>
    /// Exists because a lambda whose body is an expression returning a value infers as
    /// <see cref="Func{T}"/>, and Shouldly's throwing assertions take an <see cref="Action"/>.
    /// Funnelling it through here keeps the discard out of every test.
    /// </remarks>
    private static Action Building(Func<TransactionEventBuilder, TransactionEventBuilder> change) =>
        () => change(TransactionEventBuilder.AValidEvent()).Build();
}
