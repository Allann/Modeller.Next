using System.Collections.Immutable;

namespace Modeller.Conformance;

public sealed record ConformanceFixtureDocument(string Name, string Content);

public sealed record ConformanceFixtureCatalog(
    ImmutableArray<ConformanceFixture> Fixtures)
{
    public static ConformanceFixtureCatalog Load(
        IEnumerable<ConformanceFixtureDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var fixtures = documents
            .Select(document => ConformanceFixture.Parse(document.Content))
            .OrderBy(fixture => fixture.ScenarioId, StringComparer.Ordinal)
            .ToImmutableArray();
        var duplicate = fixtures
            .GroupBy(fixture => fixture.ScenarioId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ConformanceFixtureException(
                "fixture.scenario-id.duplicate",
                $"Conformance scenario ID '{duplicate.Key}' is duplicated.");
        }

        return new ConformanceFixtureCatalog(fixtures);
    }
}
