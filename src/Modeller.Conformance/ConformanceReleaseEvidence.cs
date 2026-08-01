using System.Collections.Immutable;

namespace Modeller.Conformance;

public sealed record ReleaseEvidenceBlocker(
    string Code,
    string Subject,
    string Message);

public sealed record ConformanceReleaseEvidence(
    bool Ready,
    ImmutableArray<ReleaseEvidenceBlocker> Blockers)
{
    public static ConformanceReleaseEvidence Evaluate(
        ConformanceEvidenceCatalog catalog,
        IEnumerable<ConformanceReport> reports,
        IEnumerable<SemanticMutationReport> mutationReports,
        IEnumerable<GeneratedConformanceFailure> generatedFailures)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(mutationReports);
        ArgumentNullException.ThrowIfNull(generatedFailures);

        var reportSet = reports.ToImmutableArray();
        var mutationSet = mutationReports.ToImmutableArray();
        var generatedFailureSet = generatedFailures.ToImmutableArray();
        var blockers = ImmutableArray.CreateBuilder<ReleaseEvidenceBlocker>();
        if (!catalog.ImplementationThresholdReady)
        {
            blockers.Add(new ReleaseEvidenceBlocker(
                "release.evidence-catalogue.incomplete",
                "evidence-catalogue",
                "The implementation-threshold evidence catalogue is incomplete."));
        }
        if (reportSet.IsEmpty)
        {
            blockers.Add(new ReleaseEvidenceBlocker(
                "release.conformance.missing",
                "conformance-reports",
                "At least one applicable conformance report is required."));
        }
        if (mutationSet.IsEmpty)
        {
            blockers.Add(new ReleaseEvidenceBlocker(
                "release.mutation.missing",
                "semantic-mutations",
                "At least one semantic mutation check is required."));
        }

        foreach (var report in reportSet
                     .Where(report => report.Status != ConformanceStatus.Passed)
                     .OrderBy(report => report.ScenarioId, StringComparer.Ordinal))
        {
            blockers.Add(new ReleaseEvidenceBlocker(
                "release.conformance.not-passed",
                report.ScenarioId,
                $"The conformance report status is {report.Status}."));
        }

        foreach (var mutation in mutationSet
                     .Where(report => report.Status != SemanticMutationStatus.Killed)
                     .OrderBy(report => report.MutationId, StringComparer.Ordinal))
        {
            blockers.Add(new ReleaseEvidenceBlocker(
                "release.mutation.not-killed",
                mutation.MutationId,
                $"The semantic mutation status is {mutation.Status}."));
        }

        foreach (var failure in generatedFailureSet
                     .OrderBy(failure => failure.GeneratorVersion, StringComparer.Ordinal)
                     .ThenBy(failure => failure.Seed))
        {
            blockers.Add(new ReleaseEvidenceBlocker(
                "release.generated-failure.unresolved",
                $"{failure.GeneratorVersion}:{failure.Seed}",
                "A generated conformance failure remains unresolved."));
        }

        var result = blockers.ToImmutable();
        return new ConformanceReleaseEvidence(result.IsEmpty, result);
    }
}
