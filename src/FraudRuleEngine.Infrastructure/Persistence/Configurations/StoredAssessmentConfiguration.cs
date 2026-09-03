using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FraudRuleEngine.Infrastructure.Persistence.Configurations;

internal sealed class StoredAssessmentConfiguration : IEntityTypeConfiguration<StoredAssessment>
{
    public void Configure(EntityTypeBuilder<StoredAssessment> builder)
    {
        builder.ToTable("fraud_assessments");

        builder.HasKey(assessment => assessment.Id);

        builder.Property(assessment => assessment.EventId)
            .HasMaxLength(EventId.MaximumLength)
            .IsRequired();

        builder.Property(assessment => assessment.TransactionId)
            .HasMaxLength(TransactionId.MaximumLength)
            .IsRequired();

        builder.Property(assessment => assessment.CustomerId)
            .HasMaxLength(CustomerId.MaximumLength)
            .IsRequired();

        builder.Property(assessment => assessment.Decision)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(assessment => assessment.RuleSetVersion)
            .HasMaxLength(RuleSetVersion.MaximumLength)
            .IsRequired();

        builder.HasOne<StoredTransaction>()
            .WithMany()
            .HasForeignKey(assessment => assessment.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // One assessment per event. Combined with the transaction table's key, a duplicate delivery
        // cannot produce a second verdict for the same event.
        builder.HasIndex(assessment => assessment.EventId)
            .HasDatabaseName("ux_fraud_assessments_event_id")
            .IsUnique();

        // Id is a column rather than only the ordering, so the keyset comparison is satisfied by the
        // index alone. Without it the planner adds an incremental sort to break ties.
        builder.HasIndex(assessment => new
            {
                assessment.CustomerId,
                assessment.EvaluatedAtUtc,
                assessment.Id,
            })
            .HasDatabaseName("ix_fraud_assessments_customer_evaluated_at_id")
            .IsDescending(false, true, true);

        // The analyst queue: everything needing attention, newest first. Ordered on evaluated time and
        // id because that is the keyset the paged query walks, and id breaks ties so a page boundary
        // cannot repeat or skip a row.
        builder.HasIndex(assessment => new
            {
                assessment.Decision,
                assessment.EvaluatedAtUtc,
                assessment.Id,
            })
            .HasDatabaseName("ix_fraud_assessments_decision_evaluated_at_id")
            .IsDescending(false, true, true);

        builder.HasIndex(assessment => new { assessment.EvaluatedAtUtc, assessment.Id })
            .HasDatabaseName("ix_fraud_assessments_evaluated_at_id")
            .IsDescending(true, true);

        builder.Navigation(assessment => assessment.RuleOutcomes).AutoInclude();
    }
}

internal sealed class StoredRuleOutcomeConfiguration : IEntityTypeConfiguration<StoredRuleOutcome>
{
    public void Configure(EntityTypeBuilder<StoredRuleOutcome> builder)
    {
        builder.ToTable("rule_outcomes");

        builder.HasKey(outcome => outcome.Id);

        builder.Property(outcome => outcome.Id).ValueGeneratedOnAdd();

        builder.Property(outcome => outcome.RuleId)
            .HasMaxLength(RuleId.MaximumLength)
            .IsRequired();

        builder.Property(outcome => outcome.Severity)
            .HasMaxLength(16)
            .IsRequired();

        // Free text, and the only column here without a tight bound. Reasons quote the values that
        // drove the decision, so they vary in length and truncating one would destroy the explanation.
        builder.Property(outcome => outcome.Reason)
            .HasMaxLength(1024)
            .IsRequired();

        builder.HasOne<StoredAssessment>()
            .WithMany(assessment => assessment.RuleOutcomes)
            .HasForeignKey(outcome => outcome.AssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Answers "which rules fire most often", which is the question an analyst tuning thresholds
        // actually asks. Filtered to the hits, since the clear rows are the large majority.
        builder.HasIndex(outcome => outcome.RuleId)
            .HasDatabaseName("ix_rule_outcomes_rule_id_triggered")
            .HasFilter("is_triggered = true");
    }
}
