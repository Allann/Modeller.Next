namespace Modeller.Conformance;

public enum SemanticMutationStatus
{
    Killed,
    Survived,
    InvalidBaseline,
    Inconclusive
}

public sealed record SemanticMutationReport(
    string MutationId,
    SemanticMutationStatus Status,
    ConformanceReport BaselineReport,
    ConformanceReport MutantReport);

public static class SemanticMutationCheck
{
    public static async ValueTask<SemanticMutationReport> VerifyAsync(
        string mutationId,
        ConformanceFixture fixture,
        IConformanceAdapter baselineAdapter,
        IConformanceAdapter mutantAdapter,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mutationId))
        {
            throw new ArgumentException("A semantic mutation ID is required.", nameof(mutationId));
        }

        var baseline = await ConformanceRunner.RunAsync(fixture, baselineAdapter, cancellationToken);
        if (baseline.Status != ConformanceStatus.Passed)
        {
            return new SemanticMutationReport(
                mutationId,
                SemanticMutationStatus.InvalidBaseline,
                baseline,
                baseline);
        }

        var mutant = await ConformanceRunner.RunAsync(fixture, mutantAdapter, cancellationToken);
        var status = mutant.Status switch
        {
            ConformanceStatus.Mismatch => SemanticMutationStatus.Killed,
            ConformanceStatus.Passed => SemanticMutationStatus.Survived,
            _ => SemanticMutationStatus.Inconclusive
        };
        return new SemanticMutationReport(mutationId, status, baseline, mutant);
    }
}
