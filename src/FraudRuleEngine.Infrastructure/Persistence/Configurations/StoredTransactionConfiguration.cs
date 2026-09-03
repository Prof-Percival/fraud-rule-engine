using FraudRuleEngine.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FraudRuleEngine.Infrastructure.Persistence.Configurations;

internal sealed class StoredTransactionConfiguration : IEntityTypeConfiguration<StoredTransaction>
{
    public void Configure(EntityTypeBuilder<StoredTransaction> builder)
    {
        builder.ToTable("transaction_events");

        // The producer's event identifier is the primary key, which makes the uniqueness that
        // idempotency depends on a property of the table rather than something the application checks.
        builder.HasKey(transaction => transaction.EventId);

        builder.Property(transaction => transaction.EventId)
            .HasMaxLength(EventId.MaximumLength);

        builder.Property(transaction => transaction.TransactionId)
            .HasMaxLength(TransactionId.MaximumLength)
            .IsRequired();

        builder.Property(transaction => transaction.CustomerId)
            .HasMaxLength(CustomerId.MaximumLength)
            .IsRequired();

        builder.Property(transaction => transaction.AccountId)
            .HasMaxLength(AccountId.MaximumLength)
            .IsRequired();

        // Nineteen digits with four decimal places, never a floating point type. Matches Money, which
        // rejects anything carrying more precision than this can hold.
        builder.Property(transaction => transaction.Amount)
            .HasPrecision(19, 4);

        builder.Property(transaction => transaction.Currency)
            .HasMaxLength(3)
            .IsRequired();

        // Enums stored as their names, not their ordinals. An ordinal in the database means renumbering
        // the enum silently rewrites the meaning of every historical row.
        builder.Property(transaction => transaction.Category)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(transaction => transaction.Channel)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(transaction => transaction.MerchantId)
            .HasMaxLength(MerchantId.MaximumLength)
            .IsRequired();

        builder.Property(transaction => transaction.MerchantName)
            .HasMaxLength(Merchant.MaximumNameLength)
            .IsRequired();

        builder.Property(transaction => transaction.CountryCode)
            .HasMaxLength(CountryCode.Length)
            .IsRequired();

        // The enrichment query is the hot path: recent transactions for one customer, ordered by time.
        // Customer first then time descending means the index answers it without a sort.
        builder.HasIndex(transaction => new { transaction.CustomerId, transaction.OccurredAtUtc })
            .HasDatabaseName("ix_transaction_events_customer_occurred_at")
            .IsDescending(false, true);

        // Supports looking a transaction up by its own identifier, which is what a support query uses.
        // Not unique: the same transaction can legitimately arrive as more than one event.
        builder.HasIndex(transaction => transaction.TransactionId)
            .HasDatabaseName("ix_transaction_events_transaction_id");
    }
}
