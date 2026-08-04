using System.Collections.Immutable;
using Modeller.Configuration;

namespace Modeller.Workspace;

/// <summary>A single document, keyed by a <see cref="Workspace.LogicalPath"/> confined to the workspace.</summary>
public sealed record WorkspaceDocument(LogicalPath Path, string Content);

/// <summary>
/// The tooling-owned, version-controlled record of durable per-document canonical identities — the
/// in-memory shape of a <c>.modeller/identities.json</c> file. Keyed by <see cref="LogicalPath"/> so
/// a registry entry for an unconfined path cannot be constructed.
/// </summary>
public sealed record WorkspaceIdentityRegistry(string Version, ImmutableDictionary<LogicalPath, ImmutableArray<string>> Documents)
{
    public static WorkspaceIdentityRegistry Empty { get; } = new("1.0", ImmutableDictionary<LogicalPath, ImmutableArray<string>>.Empty);
}

/// <summary>
/// How a workspace's documents acquire their stable identities. A closed union rather than a
/// boolean flag, so the two behaviours (mint vs. apply) are an exhaustive pattern match at the one
/// call site that needs them.
/// </summary>
public abstract record IdentityStrategy
{
    private IdentityStrategy() { }

    /// <summary>
    /// No durable registry exists yet (or the caller does not want one) — identities are minted in
    /// memory via <c>RmlCompiler.EnsureIdentities</c> and never persisted implicitly. Used for
    /// anonymous/draft workspaces such as the public playground.
    /// </summary>
    public sealed record Ephemeral : IdentityStrategy
    {
        public static Ephemeral Instance { get; } = new();
    }

    /// <summary>
    /// A durable, tooling-owned registry is authoritative; <c>RmlCompiler.ApplyIdentities</c> is used
    /// and a mismatch is a diagnostic, never a silent repair.
    /// </summary>
    public sealed record Durable(WorkspaceIdentityRegistry Registry) : IdentityStrategy;
}

/// <summary>
/// A validated, minimal workspace configuration that guarantees the two fields
/// <see cref="Modeller.Configuration.ConfigurationResolver.Resolve"/> requires exist as a matter of
/// type.
/// </summary>
public sealed record WorkspaceConfigurationInput(string GenerationContractVersion, string LogicalOutputRoot, string? Profile = null)
{
    public ConfigurationRequest ToRequest()
    {
        var baseSource = new ConfigurationSource("workspace", "1.0", ConfigurationSourceKind.Base, null,
            new Dictionary<string, ConfigurationValue>(StringComparer.Ordinal)
            {
                ["generationContractVersion"] = new(GenerationContractVersion),
                ["logicalOutputRoot"] = new(LogicalOutputRoot),
            }.ToImmutableDictionary(StringComparer.Ordinal));
        return new ConfigurationRequest([baseSource], Profile);
    }
}

/// <summary>
/// The transport-neutral input to <see cref="ModellerWorkspace"/>: a bounded set of logical-path
/// documents, an identity strategy, and resolved configuration. Directly constructible in-memory —
/// this directness is the in-memory adapter used by tests and the future hosted API.
/// </summary>
public sealed record WorkspaceInput(
    ImmutableArray<WorkspaceDocument> Documents,
    IdentityStrategy Identity,
    WorkspaceConfigurationInput Configuration);
