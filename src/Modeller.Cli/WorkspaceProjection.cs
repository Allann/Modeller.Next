using System.Text.Json;
using System.Text.Json.Serialization;
using Modeller.Model;
using Modeller.Projections;
using Modeller.Workspace;

namespace Modeller.Cli;

/// <summary>
/// The <c>project</c> capability: loads a workspace (via <see cref="WorkspaceLoader"/>, the same
/// pipeline <c>generate</c> uses) and either lists the roots a view kind can be rooted at, or
/// projects a diagram for a chosen root, via <see cref="ModellerWorkspace.Project"/>. A view kind
/// absent from <see cref="ModellerWorkspace.SupportedViewKinds"/> is rejected here with a clear
/// diagnostic rather than silently returning an empty graph. See issue #64.
/// </summary>
internal static class WorkspaceProjection
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new SemanticIdJsonConverter() },
    };

    public static async ValueTask<CliExitCode> ExecuteAsync(
        string workspace, string viewKindText, string? rootText, bool machine, ICliHost host, CancellationToken cancellationToken)
    {
        var loaded = await WorkspaceLoader.LoadAsync(workspace, host, machine, cancellationToken);
        if (loaded.Error is not null) return loaded.Error.Value;
        var revision = loaded.Value.Package.AuthoredRevision;

        if (!Enum.TryParse<ViewKind>(viewKindText, ignoreCase: true, out var viewKind) || !Enum.IsDefined(viewKind))
            return await WorkspaceLoader.Failure(host, machine, "project.view.invalid", $"'{viewKindText}' is not a recognised diagram view kind.");

        if (!ModellerWorkspace.SupportedViewKinds.Contains(viewKind))
            return await WorkspaceLoader.Failure(host, machine, "project.view.unsupported", $"The '{viewKind}' view is not implemented yet.");

        return rootText is null
            ? await EmitRootsAsync(revision, viewKind, machine, host)
            : await ProjectAsync(revision, viewKind, rootText, machine, host);
    }

    private static async ValueTask<CliExitCode> EmitRootsAsync(AuthoredContextRevision revision, ViewKind viewKind, bool machine, ICliHost host)
    {
        IEnumerable<RootSummary> candidates = viewKind switch
        {
            ViewKind.Lifecycle => revision.Definitions.OfType<EntityDefinition>().Where(entity => entity.Lifecycle is not null)
                .Select(entity => new RootSummary(entity.Id.ToString(), entity.Name.Value, entity.Slug.Value)),
            ViewKind.RuleDecision => revision.Definitions.OfType<RuleDefinition>()
                .Select(rule => new RootSummary(rule.Id.ToString(), rule.Name.Value, rule.Slug.Value)),
            ViewKind.BehaviourMap => revision.Definitions.OfType<EntityDefinition>()
                .Select(entity => new RootSummary(entity.Id.ToString(), entity.Name.Value, entity.Slug.Value)),
            ViewKind.Structural or ViewKind.CausalityAndEventFlow or ViewKind.ContextMap =>
                [new RootSummary(revision.Id.ToString(), revision.Name.Value, revision.Slug.Value)],
            _ => [],
        };
        var roots = candidates.OrderBy(root => root.Slug, StringComparer.Ordinal).ToArray();

        if (machine)
            await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", view = viewKind.ToString(), roots }, Json));
        else
            foreach (var root in roots) await host.Output.WriteLineAsync($"{root.Name} ({root.Id})");
        return CliExitCode.Success;
    }

    private static async ValueTask<CliExitCode> ProjectAsync(AuthoredContextRevision revision, ViewKind viewKind, string rootText, bool machine, ICliHost host)
    {
        if (!TryParseRoot(rootText, out var rootId))
            return await WorkspaceLoader.Failure(host, machine, "project.root.invalid", $"'{rootText}' is not a valid root identifier.");

        var result = DiagramProjector.Project(revision, new ViewDefinition($"{viewKind}:{rootId}", 1, viewKind, [rootId]));
        if (!result.Succeeded)
        {
            if (machine) await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", diagnostics = result.Diagnostics }, Json));
            else foreach (var diagnostic in result.Diagnostics) await host.Error.WriteLineAsync($"{diagnostic.Code}: {diagnostic.Message}");
            return CliExitCode.Configuration;
        }

        if (machine)
            await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", graph = result.Graph }, Json));
        else
            await host.Output.WriteLineAsync($"{result.Graph!.Nodes.Length} node(s), {result.Graph.Edges.Length} edge(s).");
        return CliExitCode.Success;
    }

    private static bool TryParseRoot(string text, out SemanticId id)
    {
        try { id = SemanticId.Parse(text); return true; }
        catch (Exception exception) when (exception is FormatException or ArgumentException) { id = default; return false; }
    }

    private sealed record RootSummary(string Id, string Name, string Slug);

    private sealed class SemanticIdJsonConverter : JsonConverter<SemanticId>
    {
        public override SemanticId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            SemanticId.Parse(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, SemanticId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
