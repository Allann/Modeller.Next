using System.Collections.Immutable;
using Modeller.Configuration;
using Modeller.Contexts;
using Modeller.Parsing;
using Modeller.Projections;

namespace Modeller.Workspace;

/// <summary>
/// The result of analyzing a <see cref="WorkspaceInput"/>: the parsed, validated package; the
/// resolved runtime configuration; source provenance; the identity strategy that was applied; and
/// the post-identity-application document text (needed by <see cref="ModellerWorkspace.Export"/>).
/// </summary>
public sealed record AnalyzedWorkspace(
    ImmutableArray<WorkspaceDocument> IdentifiedDocuments,
    LoadedContextPackage Package,
    RuntimeConfiguration Configuration,
    ImmutableArray<SourceProvenance> Provenance,
    IdentityStrategy Identity);

/// <summary>
/// The transport-neutral seam for analyzing, projecting, and exporting a Modeller workspace —
/// shared by the CLI, local Studio, a future hosted API, and tests. It assumes nothing about a
/// host filesystem, CLI process orchestration, or physical paths; see
/// docs/architecture/decisions/workspace-application-service.mdx.
/// </summary>
public static class ModellerWorkspace
{
    /// <summary>
    /// The <see cref="ViewKind"/>s <see cref="DiagramProjector"/> currently implements. See issue #64.
    /// </summary>
    public static readonly ImmutableArray<ViewKind> SupportedViewKinds =
        [ViewKind.Lifecycle, ViewKind.RuleDecision, ViewKind.Structural, ViewKind.BehaviourMap, ViewKind.CausalityAndEventFlow, ViewKind.ContextMap];

    public static WorkspaceOutcome<AnalyzedWorkspace> Analyze(WorkspaceInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (cancellationToken.IsCancellationRequested) return WorkspaceOutcome.Cancelled<AnalyzedWorkspace>();

        var configured = ConfigurationResolver.Resolve(input.Configuration.ToRequest(), cancellationToken);
        if (!configured.IsSuccess)
            return WorkspaceOutcome.Failed<AnalyzedWorkspace>(
                configured.Diagnostics.Select(diagnostic => new WorkspaceDiagnostic(diagnostic.Code, diagnostic.Message)).ToImmutableArray());

        var identified = ImmutableArray.CreateBuilder<WorkspaceDocument>(input.Documents.Length);
        foreach (var document in input.Documents)
        {
            switch (ApplyIdentity(document, input.Identity, cancellationToken))
            {
                case WorkspaceOutcome<WorkspaceDocument>.Success success:
                    identified.Add(success.Value);
                    break;
                case WorkspaceOutcome<WorkspaceDocument>.Failed failed:
                    return WorkspaceOutcome.Failed<AnalyzedWorkspace>(failed.Diagnostics);
                case WorkspaceOutcome<WorkspaceDocument>.Cancelled:
                    return WorkspaceOutcome.Cancelled<AnalyzedWorkspace>();
            }
        }

        var options = input.Identity is IdentityStrategy.Ephemeral ? ParseOptions.EditorLanguage1 : ParseOptions.Language1;
        var sources = identified.Select(document => new SourceDocument(document.Path.Value, document.Content));
        var parsed = DefinitionParser.Parse(sources, options, cancellationToken);
        if (parsed.IsCancelled) return WorkspaceOutcome.Cancelled<AnalyzedWorkspace>();
        if (!parsed.IsSuccess)
            return WorkspaceOutcome.Failed<AnalyzedWorkspace>(
                parsed.Diagnostics.Select(diagnostic => new WorkspaceDiagnostic(
                    diagnostic.Code,
                    diagnostic.Message,
                    RemapLocation(diagnostic.Location, input.Documents, identified))).ToImmutableArray());

        var remappedProvenance = parsed.Provenance.Select(item => item with
        {
            Span = RemapLocation(item.Span, input.Documents, identified) ?? item.Span
        }).ToImmutableArray();
        return WorkspaceOutcome.Success(new AnalyzedWorkspace(identified.ToImmutable(), parsed.Package!, configured.Configuration!, remappedProvenance, input.Identity));
    }

    private static SourceSpan? RemapLocation(
        SourceSpan? location,
        ImmutableArray<WorkspaceDocument> submittedDocuments,
        ImmutableArray<WorkspaceDocument>.Builder identifiedDocuments)
    {
        if (location is null) return null;
        var submitted = submittedDocuments.SingleOrDefault(document => document.Path.Value == location.Document);
        var identified = identifiedDocuments.SingleOrDefault(document => document.Path.Value == location.Document);
        if (submitted is null || identified is null) return location;

        var submittedLines = submitted.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var identifiedLines = identified.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var submittedIndex = 0;
        for (var identifiedIndex = 0; identifiedIndex < location.Line - 1 && identifiedIndex < identifiedLines.Length; identifiedIndex++)
        {
            if (submittedIndex < submittedLines.Length &&
                string.Equals(identifiedLines[identifiedIndex], submittedLines[submittedIndex], StringComparison.Ordinal))
                submittedIndex++;
        }
        return location with { Line = Math.Min(submittedIndex + 1, submittedLines.Length) };
    }

    /// <summary>Applies one document's identity per <paramref name="strategy"/> — minting via
    /// <see cref="RmlCompiler.EnsureIdentities"/> for <see cref="IdentityStrategy.Ephemeral"/>, or
    /// applying a durable registry's ordered identities via <see cref="RmlCompiler.ApplyIdentities"/>,
    /// surfacing registry coverage/mismatch as a diagnostic rather than a silent repair. Both
    /// transforms check <paramref name="cancellationToken"/> per source line, so a deadline
    /// expiring mid-document is observed promptly rather than only between documents.</summary>
    private static WorkspaceOutcome<WorkspaceDocument> ApplyIdentity(WorkspaceDocument document, IdentityStrategy strategy, CancellationToken cancellationToken)
    {
        if (strategy is IdentityStrategy.Ephemeral)
        {
            try
            {
                return WorkspaceOutcome.Success(document with { Content = RmlCompiler.EnsureIdentities(document.Content, cancellationToken).Updated });
            }
            catch (OperationCanceledException)
            {
                return WorkspaceOutcome.Cancelled<WorkspaceDocument>();
            }
        }

        var durable = (IdentityStrategy.Durable)strategy;
        if (!durable.Registry.Documents.TryGetValue(document.Path, out var identities))
            return WorkspaceOutcome.Failed<WorkspaceDocument>(
                "workspace.identity-registry.document-missing", $"The identity registry does not cover '{document.Path.Value}'.");
        try
        {
            return WorkspaceOutcome.Success(document with { Content = RmlCompiler.ApplyIdentities(document.Content, identities, cancellationToken).Updated });
        }
        catch (OperationCanceledException)
        {
            return WorkspaceOutcome.Cancelled<WorkspaceDocument>();
        }
        catch (ArgumentException)
        {
            return WorkspaceOutcome.Failed<WorkspaceDocument>(
                "workspace.identity-registry.out-of-sync", $"The identity registry is out of sync with '{document.Path.Value}'.");
        }
    }

    public static WorkspaceOutcome<ProjectionResult> Project(
        AnalyzedWorkspace analyzed, ViewDefinition view, LayoutState? layout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analyzed);
        ArgumentNullException.ThrowIfNull(view);
        if (cancellationToken.IsCancellationRequested) return WorkspaceOutcome.Cancelled<ProjectionResult>();
        if (!SupportedViewKinds.Contains(view.Kind))
            return WorkspaceOutcome.Failed<ProjectionResult>("project.view.unsupported", $"The '{view.Kind}' view is not implemented yet.");

        try
        {
            return WorkspaceOutcome.Success(DiagramProjector.Project(analyzed.Package.AuthoredRevision, view, layout, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return WorkspaceOutcome.Cancelled<ProjectionResult>();
        }
    }

    /// <summary>
    /// Harvests each analyzed document's currently-effective ordered identities — whether freshly
    /// minted (<see cref="IdentityStrategy.Ephemeral"/>) or carried through unchanged
    /// (<see cref="IdentityStrategy.Durable"/>) — into a fresh, durable
    /// <see cref="WorkspaceIdentityRegistry"/>. This is the mechanism by which an anonymous draft
    /// becomes durable; re-exporting an already-durable workspace reproduces the same registry.
    /// </summary>
    public static WorkspaceOutcome<WorkspaceIdentityRegistry> Export(AnalyzedWorkspace analyzed)
    {
        ArgumentNullException.ThrowIfNull(analyzed);
        var documents = ImmutableDictionary.CreateBuilder<LogicalPath, ImmutableArray<string>>();
        foreach (var document in analyzed.IdentifiedDocuments)
        {
            try
            {
                documents[document.Path] = RmlCompiler.HarvestIdentities(document.Content);
            }
            catch (ArgumentException exception)
            {
                return WorkspaceOutcome.Failed<WorkspaceIdentityRegistry>(
                    "workspace.identity-registry.harvest-failed", exception.Message);
            }
        }

        return WorkspaceOutcome.Success(new WorkspaceIdentityRegistry("1.0", documents.ToImmutable()));
    }
}
