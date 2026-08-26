using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

public sealed partial class WorkforceOrganisationAccessQaTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string SampleRoot = Path.Combine(RepositoryRoot, "samples", "child-care");
    private static readonly string ModelRoot = Path.Combine(SampleRoot, "model");

    private static readonly string[] WorkforceSources =
    [
        "model/entities/organisation.modeller",
        "model/entities/employee.modeller",
        "model/entities/right.modeller",
        "model/entities/rights-group.modeller",
        "model/entities/role.modeller",
        "model/entities/security-assignment.modeller",
        "model/entities/user.modeller",
        "model/entities/structure-node.modeller",
        "model/facts/workforce-access.modeller",
        "model/rules/determine-workforce-access.modeller"
    ];

    [Fact]
    public void Workforce_model_matches_the_manual_qa_scope()
    {
        AssertContains("entities/organisation.modeller", "field Name", "field Short name", "field Abbreviation");
        AssertContains("entities/user.modeller", "relationship Organisations", "target \"Organisation\"", "cardinality many");

        AssertContains(
            "entities/employee.modeller",
            "owner \"Organisation\"",
            "relationship User",
            "field External employee identifier",
            "field Name",
            "field Occupation code",
            "field Authentication subject identifier",
            "field Government person identifier",
            "field Hire date",
            "field Termination date");

        AssertContains("entities/right.modeller", "field Name", "field Description");
        AssertContains("entities/rights-group.modeller", "relationship Rights", "target \"Right\"");
        AssertContains("entities/role.modeller", "owner \"Organisation\"", "relationship Rights groups", "target \"Rights group\"");

        AssertContains(
            "entities/security-assignment.modeller",
            "owner \"Organisation\"",
            "relationship User",
            "relationship Role",
            "relationship Structure node",
            "field Effective start date",
            "field Effective end date");

        AssertContains(
            "rules/determine-workforce-access.modeller",
            "when all",
            "User is an organisation member",
            "Security assignment is current",
            "Security assignment matches exact structure node",
            "Assigned role grants required right",
            "Security assignment is organisation consistent");
    }

    [Fact]
    public void Workforce_sources_are_registered_and_have_uuidv7_identities()
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(SampleRoot, ".modeller", "config.json")));
        var sources = config.RootElement.GetProperty("sources").EnumerateArray()
            .Select(source => source.GetString())
            .ToHashSet(StringComparer.Ordinal);

        using var identities = JsonDocument.Parse(File.ReadAllText(Path.Combine(SampleRoot, ".modeller", "identities.json")));
        var documents = identities.RootElement.GetProperty("documents");

        foreach (var source in WorkforceSources)
        {
            Assert.Contains(source, sources);
            Assert.True(documents.TryGetProperty(source, out var ids), $"{source} does not have identity metadata.");
            Assert.NotEmpty(ids.EnumerateArray());
            Assert.All(ids.EnumerateArray(), id => Assert.Matches(UuidV7Pattern(), id.GetString()));
        }
    }

    [Fact]
    public void Documentation_records_scope_and_exclusions_for_issue_131()
    {
        var readme = File.ReadAllText(Path.Combine(SampleRoot, "README.md"));
        Assert.Contains("Workforce and organisation access", readme, StringComparison.Ordinal);
        Assert.Contains("exact Structure node", readme, StringComparison.Ordinal);
        Assert.Contains("does not infer access", readme, StringComparison.Ordinal);
        Assert.Contains("does not model credentials", readme, StringComparison.Ordinal);

        var gaps = File.ReadAllText(Path.Combine(SampleRoot, "gaps.md"));
        Assert.Contains("Workforce and access control", gaps, StringComparison.Ordinal);
        Assert.Contains("bounded #131 capability is ported", gaps, StringComparison.Ordinal);
        Assert.Contains("Authentication credentials", gaps, StringComparison.Ordinal);
        Assert.Contains("hierarchy-based access inheritance", gaps, StringComparison.Ordinal);
        Assert.Contains("administration", gaps, StringComparison.Ordinal);
        Assert.Contains("remain outside the bounded workforce capability", gaps, StringComparison.Ordinal);
    }

    private static void AssertContains(string relativePath, params string[] expectedValues)
    {
        var source = File.ReadAllText(Path.Combine(ModelRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (var expected in expectedValues)
        {
            Assert.Contains(expected, source, StringComparison.Ordinal);
        }
    }

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex UuidV7Pattern();
}
