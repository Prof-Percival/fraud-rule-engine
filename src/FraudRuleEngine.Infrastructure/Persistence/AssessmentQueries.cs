using FraudRuleEngine.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FraudRuleEngine.Infrastructure.Persistence;

internal sealed class AssessmentQueries : IAssessmentQueries
{
    private readonly FraudEngineDbContext _dbContext;

    public AssessmentQueries(FraudEngineDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<AssessmentView?> FindAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var stored = await _dbContext.Assessments
            .AsNoTracking()
            .Where(assessment => assessment.Id == assessmentId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return null;
        }

        var outcomes = await OutcomesFor(assessmentId).ToListAsync(cancellationToken).ConfigureAwait(false);

        return ToView(stored, outcomes);
    }

    public async Task<AssessmentPage> SearchAsync(
        AssessmentQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, AssessmentQuery.MaximumPageSize);

        var matching = Filtered(query);

        // A separate count query, which is part of what makes offset paging costly: the database has to
        // scan every matching row to answer it, on top of fetching the page.
        var total = await matching.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await matching
            .OrderByDescending(assessment => assessment.EvaluatedAtUtc)
            .ThenByDescending(assessment => assessment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(assessment => new AssessmentView
            {
                Id = assessment.Id,
                EventId = assessment.EventId,
                TransactionId = assessment.TransactionId,
                CustomerId = assessment.CustomerId,
                RiskScore = assessment.RiskScore,
                Decision = assessment.Decision,
                RuleSetVersion = assessment.RuleSetVersion,
                EvaluatedAtUtc = assessment.EvaluatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Outcomes are left off the list view deliberately. Eight rows per assessment means a page of
        // twenty five carries two hundred rows nobody reads on a list screen; the detail endpoint has
        // them.
        return new AssessmentPage
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<AssessmentSummary> SummariseAsync(CancellationToken cancellationToken)
    {
        // One grouped query rather than four counts, so the table is scanned once.
        var byDecision = await _dbContext.Assessments
            .AsNoTracking()
            .GroupBy(assessment => assessment.Decision)
            .Select(group => new { Decision = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var topRules = await _dbContext.RuleOutcomes
            .AsNoTracking()
            .Where(outcome => outcome.IsTriggered)
            .GroupBy(outcome => outcome.RuleId)
            .Select(group => new RuleHitCount { RuleId = group.Key, Hits = group.Count() })
            .OrderByDescending(hit => hit.Hits)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var counts = byDecision.ToDictionary(entry => entry.Decision, entry => entry.Count, StringComparer.Ordinal);

        return new AssessmentSummary
        {
            Total = byDecision.Sum(entry => entry.Count),
            Approved = counts.GetValueOrDefault("Approve"),
            Review = counts.GetValueOrDefault("Review"),
            Declined = counts.GetValueOrDefault("Decline"),
            MostTriggeredRules = topRules,
        };
    }

    private IQueryable<StoredAssessment> Filtered(AssessmentQuery query)
    {
        var assessments = _dbContext.Assessments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.CustomerId))
        {
            assessments = assessments.Where(assessment => assessment.CustomerId == query.CustomerId);
        }

        if (query.Decision is { } decision)
        {
            var name = decision.ToString();
            assessments = assessments.Where(assessment => assessment.Decision == name);
        }

        if (query.MinimumRiskScore is { } minimum)
        {
            assessments = assessments.Where(assessment => assessment.RiskScore >= minimum);
        }

        if (query.From is { } from)
        {
            assessments = assessments.Where(assessment => assessment.EvaluatedAtUtc >= from);
        }

        if (query.To is { } to)
        {
            assessments = assessments.Where(assessment => assessment.EvaluatedAtUtc <= to);
        }

        return assessments;
    }

    private IQueryable<RuleOutcomeView> OutcomesFor(Guid assessmentId) =>
        _dbContext.RuleOutcomes
            .AsNoTracking()
            .Where(outcome => outcome.AssessmentId == assessmentId)
            // Evaluation order, so two reads of one assessment present identically.
            .OrderBy(outcome => outcome.Ordinal)
            .Select(outcome => new RuleOutcomeView
            {
                RuleId = outcome.RuleId,
                IsTriggered = outcome.IsTriggered,
                Severity = outcome.Severity,
                Reason = outcome.Reason,
            });

    private static AssessmentView ToView(StoredAssessment stored, IReadOnlyList<RuleOutcomeView> outcomes) =>
        new()
        {
            Id = stored.Id,
            EventId = stored.EventId,
            TransactionId = stored.TransactionId,
            CustomerId = stored.CustomerId,
            RiskScore = stored.RiskScore,
            Decision = stored.Decision,
            RuleSetVersion = stored.RuleSetVersion,
            EvaluatedAtUtc = stored.EvaluatedAtUtc,
            RuleOutcomes = outcomes,
        };
}
