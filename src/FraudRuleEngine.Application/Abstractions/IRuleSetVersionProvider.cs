using FraudRuleEngine.Domain.Assessments;

namespace FraudRuleEngine.Application.Abstractions;

/// <summary>
/// Supplies the version to stamp on assessments as they are made.
/// </summary>
/// <remarks>
/// A port rather than an injected value, because thresholds become reloadable configuration later. A
/// value captured once at startup would keep reporting the version the process booted with while the
/// engine ran on newer numbers, which is worse than not recording a version at all.
/// </remarks>
public interface IRuleSetVersionProvider
{
    RuleSetVersion Current { get; }
}
