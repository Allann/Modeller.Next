using System.Collections.Immutable;
using Modeller.Model;

namespace Modeller.Contexts;

public sealed record LoadedContextPackage(
    string SchemaVersion,
    string LanguageVersion,
    AuthoredContextRevision AuthoredRevision,
    ImmutableArray<ContextImport> Imports,
    ImmutableArray<ExportedConcept> Exports,
    string SemanticDigest,
    string PackageDigest)
{
    internal ReadOnlyMemory<byte> CanonicalDocument { get; init; }
}

public sealed record ContextPackageDocument(string Name, ReadOnlyMemory<byte> Content);

public sealed record ContextPackagePersistenceResult(
    ReadOnlyMemory<byte> Document,
    string PackageDigest,
    string SemanticDigest);

public interface IContextPackageMigration
{
    string SourceSchemaVersion { get; }
    string TargetSchemaVersion { get; }

    ReadOnlyMemory<byte> Transform(
        ReadOnlyMemory<byte> sourceDocument,
        CancellationToken cancellationToken);
}

public sealed record ContextPackageMigrationStep(string SourceSchemaVersion, string TargetSchemaVersion);

public sealed record ContextPackageMigrationReport(
    string SourceSchemaVersion,
    string TargetSchemaVersion,
    string BeforeSemanticDigest,
    string AfterSemanticDigest,
    ImmutableArray<string> PreservedSemanticIds,
    ImmutableArray<ContextPackageMigrationStep> Steps);

public sealed record ContextPackageMigrationResult(
    ReadOnlyMemory<byte> Document,
    ContextPackageMigrationReport? Report,
    ImmutableArray<ContextDiagnostic> Diagnostics)
{
    public bool IsSuccess => Report is not null && Diagnostics.IsEmpty;

    internal static ContextPackageMigrationResult Failure(string code, string message) =>
        new(default, null, [new ContextDiagnostic(code, message)]);
}

public sealed record ContextImport(
    string ContextId,
    string VersionRange,
    ImmutableArray<ImportedConcept> Concepts);

public sealed record ImportedConcept(string Id, string Kind);

public sealed record ExportedConcept(string Id, string Kind);

public sealed record ContextDiagnostic(string Code, string Message);

public sealed record ContextPackageIdentity(string ContextId, string ContextVersion);

public sealed record FederationPackageLock(
    string ContextId,
    string ContextSlug,
    string ContextVersion,
    string PackageDigest,
    string SemanticDigest);

public sealed record FederationSnapshot(ImmutableArray<FederationPackageLock> Packages);

public sealed record FederationResolutionResult(
    FederationSnapshot? Snapshot,
    ImmutableArray<ContextDiagnostic> Diagnostics)
{
    public bool IsSuccess => Snapshot is not null && Diagnostics.IsEmpty;

    internal static FederationResolutionResult Failure(ContextDiagnostic diagnostic) => new(null, [diagnostic]);
}

public sealed record ContextPackageLoadResult(
    LoadedContextPackage? Package,
    ImmutableArray<ContextDiagnostic> Diagnostics)
{
    public bool IsSuccess => Package is not null && Diagnostics.IsEmpty;
}
