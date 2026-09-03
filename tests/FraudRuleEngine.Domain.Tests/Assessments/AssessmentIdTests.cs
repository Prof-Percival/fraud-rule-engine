using FraudRuleEngine.Domain.Assessments;

namespace FraudRuleEngine.Domain.Tests.Assessments;

public sealed class AssessmentIdTests
{
    [Fact]
    public void Generates_a_distinct_identifier_each_time()
    {
        AssessmentId.New().ShouldNotBe(AssessmentId.New());
    }

    [Fact]
    public void Generates_identifiers_that_cluster_by_creation_time()
    {
        // The reason for version 7 over version 4, and the claim is narrower than it first looks.
        // Ordering holds across milliseconds, not within one: inside a single millisecond the trailing
        // bits are random, so two identifiers created together have no defined order. The delays below
        // are what make this assertion meaningful rather than accidental.
        //
        // Sorted as text, not as Guid. .NET compares Guid by its internal fields rather than by bytes in
        // canonical order, so an in memory sort by Guid does not reproduce creation order. PostgreSQL
        // compares uuid by bytes, which is where the clustering actually pays off.
        var first = AssessmentId.New();
        Thread.Sleep(2);
        var second = AssessmentId.New();
        Thread.Sleep(2);
        var third = AssessmentId.New();

        var sorted = new[] { third, first, second }
            .OrderBy(id => id.Value.ToString(), StringComparer.Ordinal)
            .ToArray();

        sorted.ShouldBe([first, second, third]);
    }

    [Fact]
    public void Round_trips_an_identifier_read_back_from_storage()
    {
        var original = AssessmentId.New();

        AssessmentId.From(original.Value).ShouldBe(original);
    }

    [Fact]
    public void Rejects_an_empty_identifier()
    {
        var act = () => { _ = AssessmentId.From(Guid.Empty); };

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("value");
    }

    [Fact]
    public void A_defaulted_identifier_reports_itself_as_uninitialised()
    {
        default(AssessmentId).IsInitialised.ShouldBeFalse();
        AssessmentId.New().IsInitialised.ShouldBeTrue();
    }
}
