using System.Reflection;

namespace FraudRuleEngine.Domain.Tests;

/// <summary>
/// Guards the dependency rule from docs/adr/0001-layered-architecture.md.
///
/// The domain project having no infrastructure dependencies is the property the whole
/// layering argument rests on, and a claim in a markdown file is not a guarantee. This is,
/// so that the build fails the moment somebody adds an EF Core reference to the domain to
/// get something working quickly.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.DependencyInjection",
        "Npgsql",
        "Serilog",
        "OpenTelemetry",
    ];

    [Fact]
    public void Domain_references_no_infrastructure_assemblies()
    {
        var domain = Assembly.Load("FraudRuleEngine.Domain");

        var violations = domain
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => ForbiddenPrefixes.Any(prefix =>
                name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        violations.ShouldBeEmpty(
            "the domain must not depend on persistence, hosting or observability packages");
    }

    [Fact]
    public void Domain_references_no_other_project_in_the_solution()
    {
        var domain = Assembly.Load("FraudRuleEngine.Domain");

        var violations = domain
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("FraudRuleEngine.", StringComparison.Ordinal))
            .ToArray();

        violations.ShouldBeEmpty("the domain sits at the centre and depends on nothing inward of it");
    }
}
