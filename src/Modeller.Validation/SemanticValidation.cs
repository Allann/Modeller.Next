using System.Collections.Immutable;
using Modeller.Contexts;
using Modeller.Model;

namespace Modeller.Validation;

public static class SemanticValidation
{
    private static readonly ImmutableArray<ValidationStage> CanonicalStages =
    [
        ValidationStage.Decode,
        ValidationStage.IdentityAndOwnership,
        ValidationStage.ReferencesAndFederation,
        ValidationStage.TypesAndExpressions,
        ValidationStage.RulesAndDecisions,
        ValidationStage.Behaviours,
        ValidationStage.Lifecycles,
        ValidationStage.Views
    ];

    public static ValidationResult Validate(
        ValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            var cancelledStages = CanonicalStages.Select((stage, index) => new ValidationStageResult(
                stage,
                index == 0 ? ValidationStageStatus.Cancelled : ValidationStageStatus.Skipped,
                [])).ToImmutableArray();
            return new ValidationResult(cancelledStages, []);
        }

        var revisions = request.Subject switch
        {
            ContextPackageValidationSubject package => ImmutableArray.Create(package.Package.AuthoredRevision),
            AuthoredContextValidationSubject authored => ImmutableArray.Create(authored.Revision),
            FederationSnapshotValidationSubject snapshot => snapshot.Packages
                .Select(package => package.AuthoredRevision)
                .ToImmutableArray(),
            _ => throw new ArgumentException("The validation subject is not supported.", nameof(request))
        };
        var decodeApplicable = request.Subject is not AuthoredContextValidationSubject;
        var diagnostics = request.Profile.WorkBudget <= 1
            ? ImmutableArray.Create(new ValidationDiagnostic(
                "validation.limit.work-exceeded",
                ValidationStage.IdentityAndOwnership,
                ValidationSeverity.Error,
                "Validation exceeded the deterministic work budget."))
                .ToBuilder()
            : revisions.SelectMany(revision => ValidateReferences(revision)).ToImmutableArray().ToBuilder();
        if (request.Subject is FederationSnapshotValidationSubject snapshotSubject)
        {
            diagnostics.AddRange(ValidateSnapshotLocks(snapshotSubject));
        }
        if (!diagnostics.Any(diagnostic => diagnostic.Severity == ValidationSeverity.Error))
        {
            foreach (var extension in request.Extensions
                         .OrderBy(extension => extension.Id, StringComparer.Ordinal)
                         .ThenBy(extension => extension.Version, StringComparer.Ordinal))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Cancelled();
                }

                try
                {
                    var extensionDiagnostics = extension.Validate(
                            new ValidationExtensionContext(request.Subject, request.Profile),
                            cancellationToken)
                        .Take(33)
                        .ToArray();
                    if (extensionDiagnostics.Length > 32 || extensionDiagnostics.Any(diagnostic =>
                            diagnostic.Stage != extension.Stage ||
                            !diagnostic.Code.StartsWith($"extension.{extension.Id}.", StringComparison.Ordinal)))
                    {
                        diagnostics.Add(ExtensionFailure(
                            extension,
                            "validation.extension.output-invalid",
                            "The validation extension returned invalid or excessive diagnostics."));
                    }
                    else
                    {
                        diagnostics.AddRange(extensionDiagnostics);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return Cancelled();
                }
                catch (Exception)
                {
                    diagnostics.Add(ExtensionFailure(
                        extension,
                        "validation.extension.failed",
                        "The validation extension failed without producing a trusted result."));
                }
            }
        }

        var allOrderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.Stage)
            .ThenBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToImmutableArray();
        var orderedDiagnostics = allOrderedDiagnostics.Length <= request.Profile.MaximumDiagnostics
            ? allOrderedDiagnostics
            : allOrderedDiagnostics.Take(request.Profile.MaximumDiagnostics - 1)
                .Append(new ValidationDiagnostic(
                    "validation.limit.diagnostics-exceeded",
                    allOrderedDiagnostics[request.Profile.MaximumDiagnostics - 1].Stage,
                    ValidationSeverity.Error,
                    "Validation produced more diagnostics than the profile permits."))
                .ToImmutableArray();
        var failedStage = orderedDiagnostics
            .Where(diagnostic => diagnostic.Severity == ValidationSeverity.Error)
            .Select(diagnostic => (ValidationStage?)diagnostic.Stage)
            .FirstOrDefault();
        var stages = CanonicalStages.Select(stage => new ValidationStageResult(
            stage,
            stage == ValidationStage.Decode && !decodeApplicable
                ? ValidationStageStatus.Skipped
                : failedStage is null || (int)stage < (int)failedStage.Value
                ? ValidationStageStatus.Passed
                : stage == failedStage
                    ? ValidationStageStatus.Failed
                    : ValidationStageStatus.Skipped,
            orderedDiagnostics.Where(diagnostic => diagnostic.Stage == stage).ToImmutableArray())).ToImmutableArray();
        return new ValidationResult(stages, orderedDiagnostics);
    }

    private static ValidationResult Cancelled()
    {
        var stages = CanonicalStages.Select((stage, index) => new ValidationStageResult(
            stage,
            index == 0 ? ValidationStageStatus.Cancelled : ValidationStageStatus.Skipped,
            [])).ToImmutableArray();
        return new ValidationResult(stages, []);
    }

    private static ValidationDiagnostic ExtensionFailure(
        IValidationExtension extension,
        string code,
        string message) =>
        new(code, extension.Stage, ValidationSeverity.Error, message);

    private static ImmutableArray<ValidationDiagnostic> ValidateReferences(
        AuthoredContextRevision revision)
    {
        var diagnostics = ImmutableArray.CreateBuilder<ValidationDiagnostic>();
        foreach (var rule in revision.Definitions.OfType<RuleDefinition>().OrderBy(rule => rule.Id.ToString(), StringComparer.Ordinal))
        {
            for (var index = 0; index < rule.InputFacts.Length; index++)
            {
                var reference = rule.InputFacts[index];
                var target = revision.FindConcept(reference.TargetId);
                if (target?.Kind != SemanticKind.Fact)
                {
                    diagnostics.Add(new ValidationDiagnostic(
                        "validation.reference.fact-unresolved",
                        ValidationStage.ReferencesAndFederation,
                        ValidationSeverity.Error,
                        "The rule input must reference a declared Fact.",
                        rule.Id.ToString(),
                        $"/definitions/{rule.Id}/inputFacts/{index}"));
                }
            }
        }

        foreach (var behaviour in revision.Definitions.OfType<BehaviourDefinition>()
                     .OrderBy(behaviour => behaviour.Id.ToString(), StringComparer.Ordinal))
        {
            foreach (var transition in behaviour.Transitions.OrderBy(
                         transition => transition.Id.ToString(),
                         StringComparer.Ordinal))
            {
                var outcome = revision.FindConcept(transition.Outcome.TargetId);
                if (outcome?.Kind != SemanticKind.Outcome)
                {
                    diagnostics.Add(new ValidationDiagnostic(
                        "validation.reference.outcome-unresolved",
                        ValidationStage.ReferencesAndFederation,
                        ValidationSeverity.Error,
                        "The transition must reference an Outcome declared by its Behaviour.",
                        transition.Id.ToString(),
                        $"/definitions/{behaviour.Id}/transitions/{transition.Id}/outcome"));
                }
            }
        }

        return diagnostics.OrderBy(diagnostic => diagnostic.Path, StringComparer.Ordinal).ToImmutableArray();
    }

    private static ImmutableArray<ValidationDiagnostic> ValidateSnapshotLocks(
        FederationSnapshotValidationSubject subject)
    {
        var supplied = subject.Packages.ToDictionary(
            package => package.AuthoredRevision.Id.ToString(),
            StringComparer.Ordinal);
        var diagnostics = ImmutableArray.CreateBuilder<ValidationDiagnostic>();
        foreach (var locked in subject.Snapshot.Packages.OrderBy(item => item.ContextId, StringComparer.Ordinal))
        {
            if (!supplied.TryGetValue(locked.ContextId, out var package) ||
                package.AuthoredRevision.ContextVersion != locked.ContextVersion ||
                package.PackageDigest != locked.PackageDigest ||
                package.SemanticDigest != locked.SemanticDigest)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    "validation.federation.lock-mismatch",
                    ValidationStage.ReferencesAndFederation,
                    ValidationSeverity.Error,
                    "The supplied context package does not match its exact federation snapshot lock.",
                    locked.ContextId,
                    $"/packages/{locked.ContextId}"));
            }
        }

        return diagnostics.ToImmutable();
    }
}

public sealed record ValidationRequest(
    ValidationSubject Subject,
    ValidationProfile Profile,
    ImmutableArray<IValidationExtension> Extensions)
{
    public static ValidationRequest For(
        LoadedContextPackage package,
        ValidationProfile profile,
        IEnumerable<IValidationExtension>? extensions = null) =>
        new(
            new ContextPackageValidationSubject(package),
            profile,
            extensions?.ToImmutableArray() ?? []);

    public static ValidationRequest For(
        AuthoredContextRevision revision,
        ValidationProfile profile,
        IEnumerable<IValidationExtension>? extensions = null) =>
        new(
            new AuthoredContextValidationSubject(revision),
            profile,
            extensions?.ToImmutableArray() ?? []);

    public static ValidationRequest For(
        FederationSnapshot snapshot,
        IEnumerable<LoadedContextPackage> packages,
        ValidationProfile profile,
        IEnumerable<IValidationExtension>? extensions = null) =>
        new(
            new FederationSnapshotValidationSubject(snapshot, packages.ToImmutableArray()),
            profile,
            extensions?.ToImmutableArray() ?? []);
}

public abstract record ValidationSubject;

public sealed record ContextPackageValidationSubject(LoadedContextPackage Package) : ValidationSubject;

public sealed record AuthoredContextValidationSubject(AuthoredContextRevision Revision) : ValidationSubject;

public sealed record FederationSnapshotValidationSubject(
    FederationSnapshot Snapshot,
    ImmutableArray<LoadedContextPackage> Packages) : ValidationSubject;

public sealed record ValidationProfile
{
    public ValidationProfile(string name, int maximumDiagnostics, int workBudget)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A validation profile name is required.", nameof(name))
            : name.Trim();
        MaximumDiagnostics = maximumDiagnostics > 0
            ? maximumDiagnostics
            : throw new ArgumentOutOfRangeException(nameof(maximumDiagnostics));
        WorkBudget = workBudget > 0
            ? workBudget
            : throw new ArgumentOutOfRangeException(nameof(workBudget));
    }

    public string Name { get; }
    public int MaximumDiagnostics { get; }
    public int WorkBudget { get; }

    public static ValidationProfile Canonical { get; } = new("Canonical", 256, 100_000);
}

public sealed record ValidationExtensionContext(
    ValidationSubject Subject,
    ValidationProfile Profile);

public interface IValidationExtension
{
    string Id { get; }
    string Version { get; }
    ValidationStage Stage { get; }

    IEnumerable<ValidationDiagnostic> Validate(
        ValidationExtensionContext context,
        CancellationToken cancellationToken);
}

public enum ValidationStage
{
    Decode,
    IdentityAndOwnership,
    ReferencesAndFederation,
    TypesAndExpressions,
    RulesAndDecisions,
    Behaviours,
    Lifecycles,
    Views
}

public enum ValidationStageStatus
{
    Passed,
    Failed,
    Skipped,
    Cancelled
}

public enum ValidationSeverity
{
    Information,
    Warning,
    Error
}

public sealed record ValidationDiagnostic(
    string Code,
    ValidationStage Stage,
    ValidationSeverity Severity,
    string Message,
    string? SubjectId = null,
    string? Path = null);

public sealed record ValidationStageResult(
    ValidationStage Stage,
    ValidationStageStatus Status,
    ImmutableArray<ValidationDiagnostic> Diagnostics);

public sealed record ValidationResult(
    ImmutableArray<ValidationStageResult> Stages,
    ImmutableArray<ValidationDiagnostic> Diagnostics)
{
    public bool IsCancelled => Stages.Any(stage => stage.Status == ValidationStageStatus.Cancelled);
    public bool IsValid => !IsCancelled && Diagnostics.All(diagnostic => diagnostic.Severity != ValidationSeverity.Error);
}
