using System.Collections.Immutable;

namespace Modeller.Contexts;

public static partial class ContextPackageSystem
{
    public static FederationResolutionResult Resolve(
        IEnumerable<ReadOnlyMemory<byte>> documents,
        ContextPackageIdentity root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(root);

        var packages = new List<LoadedContextPackage>();
        foreach (var document in documents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return FederationResolutionResult.Failure(
                    new ContextDiagnostic("package.resolution.cancelled", "Context-package resolution was cancelled."));
            }

            var loaded = Load(document);
            if (!loaded.IsSuccess)
            {
                return new FederationResolutionResult(null, loaded.Diagnostics);
            }

            packages.Add(loaded.Package!);
        }

        var mutableVersion = packages
            .GroupBy(
                package => (package.AuthoredRevision.Id, package.AuthoredRevision.ContextVersion))
            .FirstOrDefault(group => group.Select(package => package.PackageDigest).Distinct().Skip(1).Any());
        if (mutableVersion is not null)
        {
            return FederationResolutionResult.Failure(new ContextDiagnostic(
                "package.version.mutable",
                $"Published context package '{mutableVersion.Key.Id}@{mutableVersion.Key.ContextVersion}' has multiple package digests."));
        }

        packages = packages
            .DistinctBy(package =>
                (package.AuthoredRevision.Id, package.AuthoredRevision.ContextVersion, package.PackageDigest))
            .ToList();

        var selected = packages.SingleOrDefault(package =>
            package.AuthoredRevision.Id.ToString() == root.ContextId &&
            package.AuthoredRevision.ContextVersion == root.ContextVersion);
        if (selected is null)
        {
            return FederationResolutionResult.Failure(
                new ContextDiagnostic(
                    "package.root.unresolved",
                    $"Context package '{root.ContextId}@{root.ContextVersion}' was not supplied."));
        }

        var resolved = new Dictionary<string, LoadedContextPackage>(StringComparer.Ordinal)
        {
            [selected.AuthoredRevision.Id.ToString()] = selected
        };
        var pending = new Queue<LoadedContextPackage>([selected]);
        var dependencyEdges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var consumer))
        {
            foreach (var import in consumer.Imports)
            {
                if (!IsValidVersionRange(import.VersionRange))
                {
                    return FederationResolutionResult.Failure(new ContextDiagnostic(
                        "package.import.version-range-invalid",
                        $"Import version range '{import.VersionRange}' is not an explicit supported range."));
                }

                var candidates = packages.Where(package =>
                        package.AuthoredRevision.Id.ToString() == import.ContextId &&
                        VersionRangeIncludes(import.VersionRange, package.AuthoredRevision.ContextVersion))
                    .OrderBy(package => package.AuthoredRevision.ContextVersion, StringComparer.Ordinal)
                    .ToArray();
                if (candidates.Length != 1)
                {
                    var code = candidates.Length > 1
                        ? "package.import.ambiguous"
                        : packages.Any(package => package.AuthoredRevision.Id.ToString() == import.ContextId)
                            ? "package.import.incompatible"
                            : "package.import.unresolved";
                    return FederationResolutionResult.Failure(new ContextDiagnostic(
                        code,
                        $"Import '{import.ContextId}@{import.VersionRange}' resolved to {candidates.Length} packages."));
                }

                var provider = candidates[0];
                var consumerId = consumer.AuthoredRevision.Id.ToString();
                if (HasPath(dependencyEdges, import.ContextId, consumerId))
                {
                    return FederationResolutionResult.Failure(new ContextDiagnostic(
                        "package.import.cycle",
                        $"Importing '{import.ContextId}' from '{consumerId}' would create a cycle."));
                }

                if (!dependencyEdges.TryGetValue(consumerId, out var dependencies))
                {
                    dependencies = [];
                    dependencyEdges.Add(consumerId, dependencies);
                }

                dependencies.Add(import.ContextId);
                foreach (var concept in import.Concepts)
                {
                    var exported = provider.Exports.SingleOrDefault(item => item.Id == concept.Id);
                    if (exported is null)
                    {
                        return FederationResolutionResult.Failure(new ContextDiagnostic(
                            "package.export.unresolved",
                            $"Concept '{concept.Id}' is not exported by '{import.ContextId}'."));
                    }

                    if (!string.Equals(exported.Kind, concept.Kind, StringComparison.Ordinal))
                    {
                        return FederationResolutionResult.Failure(new ContextDiagnostic(
                            "package.export.kind-mismatch",
                            $"Concept '{concept.Id}' is exported as '{exported.Kind}', not '{concept.Kind}'."));
                    }
                }

                if (resolved.TryAdd(import.ContextId, provider))
                {
                    pending.Enqueue(provider);
                }
            }
        }

        var locked = resolved.Values
            .OrderBy(package => package.AuthoredRevision.Id.ToString(), StringComparer.Ordinal)
            .Select(package => new FederationPackageLock(
                package.AuthoredRevision.Id.ToString(),
                package.AuthoredRevision.Slug.Value,
                package.AuthoredRevision.ContextVersion,
                package.PackageDigest,
                package.SemanticDigest))
            .ToImmutableArray();
        return new FederationResolutionResult(new FederationSnapshot(locked), []);
    }

    private static bool HasPath(
        IReadOnlyDictionary<string, HashSet<string>> edges,
        string source,
        string target)
    {
        var pending = new Stack<string>([source]);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryPop(out var current))
        {
            if (current == target)
            {
                return true;
            }

            if (visited.Add(current) && edges.TryGetValue(current, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    pending.Push(dependency);
                }
            }
        }

        return false;
    }
}
