using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Domain.Assessments;

namespace FraudRuleEngine.Infrastructure.Configuration;

/// <summary>
/// Reports a single compiled version.
/// </summary>
/// <remarks>
/// Honest only while the thresholds are themselves compiled in, which they currently are. Once rule
/// configuration is bound and reloadable, the version has to derive from that configuration, or an
/// assessment would claim a version whose numbers it was not actually scored under.
/// </remarks>
internal sealed class FixedRuleSetVersionProvider : IRuleSetVersionProvider
{
    public RuleSetVersion Current { get; } = RuleSetVersion.From("compiled-1");
}
