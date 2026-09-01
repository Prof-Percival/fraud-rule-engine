using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.TestSupport;

/// <summary>
/// Builds a valid <see cref="TransactionEvent"/> that individual tests then vary one field of.
/// </summary>
/// <remarks>
/// The point is that a test says only what it cares about. A velocity test should read as "three
/// transactions two minutes apart" without also stating a merchant name, a country and an account
/// number that have nothing to do with what is being asserted. Every field has a sensible default
/// and tests override the one under examination.
///
/// <para>
/// Deliberately not a mocking framework. The domain has no dependencies to mock, and a hand written
/// builder produces tests that read as prose, which matters more here than saving a few lines.
/// </para>
/// </remarks>
internal sealed class TransactionEventBuilder
{
    private EventId _eventId = EventId.From("evt-00000000-0000-0000-0000-000000000001");
    private TransactionId _transactionId = TransactionId.From("TXN-000001");
    private CustomerId _customerId = CustomerId.From("CUST-0001");
    private AccountId _accountId = AccountId.From("ACC-0001");
    private Money _amount = new(250.00m, Currency.Zar);
    private TransactionCategory _category = TransactionCategory.Groceries;
    private TransactionChannel _channel = TransactionChannel.ChipAndPin;
    private Merchant _merchant = new(MerchantId.From("MERCH-0001"), "Checkers Hyper Constantia");
    private CountryCode _country = CountryCode.From("ZA");
    private DateTimeOffset _occurredAt = new(2026, 9, 1, 14, 30, 0, TimeSpan.Zero);

    public static TransactionEventBuilder AValidEvent() => new();

    public TransactionEventBuilder WithEventId(EventId eventId)
    {
        _eventId = eventId;
        return this;
    }

    public TransactionEventBuilder WithTransactionId(TransactionId transactionId)
    {
        _transactionId = transactionId;
        return this;
    }

    public TransactionEventBuilder WithCustomerId(CustomerId customerId)
    {
        _customerId = customerId;
        return this;
    }

    public TransactionEventBuilder WithAccountId(AccountId accountId)
    {
        _accountId = accountId;
        return this;
    }

    public TransactionEventBuilder WithAmount(Money amount)
    {
        _amount = amount;
        return this;
    }

    public TransactionEventBuilder WithAmount(decimal amount, Currency currency = Currency.Zar)
    {
        _amount = new Money(amount, currency);
        return this;
    }

    public TransactionEventBuilder WithCategory(TransactionCategory category)
    {
        _category = category;
        return this;
    }

    public TransactionEventBuilder WithChannel(TransactionChannel channel)
    {
        _channel = channel;
        return this;
    }

    public TransactionEventBuilder WithMerchant(Merchant merchant)
    {
        _merchant = merchant;
        return this;
    }

    public TransactionEventBuilder WithCountry(CountryCode country)
    {
        _country = country;
        return this;
    }

    public TransactionEventBuilder WithCountry(string country)
    {
        _country = CountryCode.From(country);
        return this;
    }

    public TransactionEventBuilder OccurringAt(DateTimeOffset occurredAt)
    {
        _occurredAt = occurredAt;
        return this;
    }

    public TransactionEvent Build() => new(
        _eventId,
        _transactionId,
        _customerId,
        _accountId,
        _amount,
        _category,
        _channel,
        _merchant,
        _country,
        _occurredAt);
}
