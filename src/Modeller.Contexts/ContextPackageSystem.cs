using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Modeller.Model;

namespace Modeller.Contexts;

public static class ContextPackageSystem
{
    public static ContextPackageLoadResult Load(IEnumerable<ContextPackageDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var fragments = documents.ToArray();
        if (fragments.Length == 0)
        {
            return new ContextPackageLoadResult(
                null,
                [new ContextDiagnostic("package.document.required", "At least one context-package document is required.")]);
        }

        foreach (var fragment in fragments)
        {
            var normalizedName = fragment.Name.Replace('\\', '/');
            if (Path.IsPathRooted(fragment.Name) ||
                normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
            {
                return new ContextPackageLoadResult(
                    null,
                    [new ContextDiagnostic("package.path.invalid", "Package document names must remain within the package.")]);
            }
        }

        try
        {
            var roots = fragments.Select(fragment => JsonNode.Parse(fragment.Content.Span)!.AsObject()).ToArray();
            var first = roots[0];
            var identity = FragmentIdentity(first);
            if (roots.Skip(1).Any(root => FragmentIdentity(root) != identity))
            {
                return new ContextPackageLoadResult(
                    null,
                    [new ContextDiagnostic(
                        "package.fragment.identity-mismatch",
                        "Every package fragment must declare the same schema, language, context identity, and context version.")]);
            }

            var duplicateDefinition = roots
                .SelectMany(root => root["definitions"]!.AsArray())
                .GroupBy(definition => definition!["id"]!.GetValue<string>(), StringComparer.Ordinal)
                .FirstOrDefault(group => group.Skip(1).Any());
            if (duplicateDefinition is not null)
            {
                return new ContextPackageLoadResult(
                    null,
                    [new ContextDiagnostic(
                        "package.identity.duplicate",
                        $"Semantic identity '{duplicateDefinition.Key}' is defined by multiple package fragments.")]);
            }

            var combined = first.DeepClone().AsObject();
            combined["imports"] = MergeArrays(roots, "imports", ImportKey);
            combined["exports"] = MergeArrays(roots, "exports", node => node!.GetValue<string>());
            combined["definitions"] = MergeArrays(roots, "definitions", node => node!["id"]!.GetValue<string>());
            combined.Remove("layout");
            combined.Remove("layoutState");
            combined.Remove("provenance");
            combined.Remove("sourceProvenance");

            var result = Load(JsonSerializer.SerializeToUtf8Bytes(combined));
            if (!result.IsSuccess)
            {
                return result;
            }

            var manifest = string.Join(
                '\n',
                fragments.OrderBy(fragment => fragment.Name, StringComparer.Ordinal)
                    .Select(fragment => $"{fragment.Name}:{Digest(fragment.Content.Span)}"));
            return result with
            {
                Package = result.Package! with
                {
                    PackageDigest = Digest(Encoding.UTF8.GetBytes(manifest))
                }
            };
        }
        catch (JsonException)
        {
            return new ContextPackageLoadResult(
                null,
                [new ContextDiagnostic("package.document.invalid", "A package fragment is not valid JSON.")]);
        }
    }

    public static ContextPackageLoadResult Load(ReadOnlyMemory<byte> document)
    {
        if (document.Length > 5 * 1024 * 1024)
        {
            return new ContextPackageLoadResult(
                null,
                [new ContextDiagnostic("package.limit.size", "A context package cannot exceed 5 MiB.")]);
        }

        try
        {
            using var json = JsonDocument.Parse(document, new JsonDocumentOptions { MaxDepth = 64 });
            var root = json.RootElement;
            var duplicateProperty = FindDuplicateProperty(root, string.Empty);
            if (duplicateProperty is not null)
            {
                return new ContextPackageLoadResult(
                    null,
                    [new ContextDiagnostic(
                        "package.property.duplicate",
                        $"JSON property '{duplicateProperty}' is declared more than once.")]);
            }

            var maliciousPath = FindMaliciousPath(root);
            if (maliciousPath is not null)
            {
                return new ContextPackageLoadResult(
                    null,
                    [new ContextDiagnostic(
                        "package.path.invalid",
                        $"Package path '{maliciousPath}' must be relative and remain within its package.")]);
            }

            var schemaVersion = root.GetProperty("schemaVersion").GetString()!;
            if (!string.Equals(schemaVersion, "1.0", StringComparison.Ordinal))
            {
                return new ContextPackageLoadResult(
                    null,
                    [new ContextDiagnostic(
                        "package.schema.unsupported",
                        $"Persistence schema '{schemaVersion}' is not supported; an explicit migration may be required.")]);
            }

            var context = root.GetProperty("context");
            var revision = AuthoredContextRevision.Create(
                SemanticId.Parse(context.GetProperty("id").GetString()!),
                new SemanticName(context.GetProperty("name").GetString()!),
                new SemanticSlug(context.GetProperty("slug").GetString()!),
                context.GetProperty("version").GetString()!);

            foreach (var definition in root.GetProperty("definitions").EnumerateArray())
            {
                var addition = CanonicalModel.Apply(revision, new AddDefinition(ReadDefinition(definition)));
                if (!addition.Succeeded)
                {
                    var diagnostic = addition.Diagnostics[0];
                    var code = diagnostic.Code == "model.identity.duplicate"
                        ? "package.identity.duplicate"
                        : "package.definition.invalid";
                    return new ContextPackageLoadResult(
                        null,
                        [new ContextDiagnostic(code, diagnostic.Message)]);
                }

                revision = addition.Revision;
            }

            var canonicalDocument = Canonicalize(root, semantic: false);
            var semanticDigest = Digest(Canonicalize(root));
            if (root.TryGetProperty("semanticDigest", out var declaredSemanticDigest) &&
                !string.Equals(declaredSemanticDigest.GetString(), semanticDigest, StringComparison.Ordinal))
            {
                return new ContextPackageLoadResult(
                    null,
                    [new ContextDiagnostic(
                        "package.digest.semantic-mismatch",
                        "The declared semantic digest does not match normalized package meaning.")]);
            }

            var package = new LoadedContextPackage(
                schemaVersion,
                root.GetProperty("languageVersion").GetString()!,
                revision,
                ReadImports(root),
                ReadExports(root, revision),
                semanticDigest,
                Digest(document.Span))
            {
                CanonicalDocument = canonicalDocument
            };

            return new ContextPackageLoadResult(package, []);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or ArgumentException)
        {
            return new ContextPackageLoadResult(
                null,
                [new ContextDiagnostic("package.document.invalid", exception.Message)]);
        }
    }

    private static string FragmentIdentity(JsonObject root)
    {
        var context = root["context"]!.AsObject();
        return string.Join(
            '|',
            root["schemaVersion"]!.GetValue<string>(),
            root["languageVersion"]!.GetValue<string>(),
            context["id"]!.GetValue<string>(),
            context["version"]!.GetValue<string>());
    }

    private static JsonArray MergeArrays(
        IEnumerable<JsonObject> roots,
        string propertyName,
        Func<JsonNode?, string> key) =>
        new(roots.SelectMany(root => root[propertyName]!.AsArray())
            .GroupBy(key, StringComparer.Ordinal)
            .Select(group => group.First()!.DeepClone())
            .ToArray());

    private static string ImportKey(JsonNode? import)
    {
        var value = import!.AsObject();
        return $"{value["contextId"]!.GetValue<string>()}|{value["versionRange"]!.GetValue<string>()}";
    }

    private static string? FindDuplicateProperty(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{path}/{property.Name}";
                if (!names.Add(property.Name))
                {
                    return propertyPath;
                }

                var nested = FindDuplicateProperty(property.Value, propertyPath);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindDuplicateProperty(item, $"{path}/{index++}");
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? FindMaliciousPath(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.EndsWith("Path", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    var path = property.Value.GetString()!;
                    var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (Path.IsPathRooted(path) || segments.Contains("..", StringComparer.Ordinal))
                    {
                        return path;
                    }
                }

                var nested = FindMaliciousPath(property.Value);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindMaliciousPath(item);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

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

    public static ContextPackagePersistenceResult Persist(LoadedContextPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var document = package.CanonicalDocument.ToArray();
        return new ContextPackagePersistenceResult(document, Digest(document), package.SemanticDigest);
    }

    public static ContextPackageMigrationResult Migrate(
        ReadOnlyMemory<byte> document,
        string targetSchemaVersion,
        IEnumerable<IContextPackageMigration> migrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetSchemaVersion);
        ArgumentNullException.ThrowIfNull(migrations);

        try
        {
            using var originalJson = JsonDocument.Parse(document);
            var sourceVersion = originalJson.RootElement.GetProperty("schemaVersion").GetString()!;
            var beforeDigest = Digest(Canonicalize(originalJson.RootElement));
            var beforeIds = SemanticIds(originalJson.RootElement);
            var currentVersion = sourceVersion;
            var currentDocument = document.ToArray().AsMemory();
            var available = migrations.ToArray();
            var steps = ImmutableArray.CreateBuilder<ContextPackageMigrationStep>();

            for (var index = 0; currentVersion != targetSchemaVersion && index < 16; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ContextPackageMigrationResult.Failure("package.migration.cancelled", "Migration was cancelled.");
                }

                var candidates = available.Where(item => item.SourceSchemaVersion == currentVersion).ToArray();
                if (candidates.Length != 1)
                {
                    return ContextPackageMigrationResult.Failure(
                        candidates.Length == 0 ? "package.migration.required" : "package.migration.ambiguous",
                        $"Schema '{currentVersion}' has {candidates.Length} explicit migration steps.");
                }

                var migration = candidates[0];
                var transformed = migration.Transform(currentDocument, cancellationToken).ToArray().AsMemory();
                using var transformedJson = JsonDocument.Parse(transformed);
                var declaredTarget = transformedJson.RootElement.GetProperty("schemaVersion").GetString()!;
                if (declaredTarget != migration.TargetSchemaVersion)
                {
                    return ContextPackageMigrationResult.Failure(
                        "package.migration.target-mismatch",
                        $"Migration declared '{migration.TargetSchemaVersion}' but produced '{declaredTarget}'.");
                }

                steps.Add(new ContextPackageMigrationStep(currentVersion, declaredTarget));
                currentVersion = declaredTarget;
                currentDocument = transformed;
            }

            if (currentVersion != targetSchemaVersion)
            {
                return ContextPackageMigrationResult.Failure(
                    "package.migration.limit.exceeded",
                    "Migration did not reach its requested schema within 16 steps.");
            }

            var loaded = Load(currentDocument);
            if (!loaded.IsSuccess)
            {
                return new ContextPackageMigrationResult(default, null, loaded.Diagnostics);
            }

            if (loaded.Package!.SemanticDigest != beforeDigest)
            {
                return ContextPackageMigrationResult.Failure(
                    "package.migration.meaning-changed",
                    "A schema migration changed the package's semantic digest.");
            }

            using var finalJson = JsonDocument.Parse(currentDocument);
            var afterIds = SemanticIds(finalJson.RootElement);
            if (!beforeIds.SetEquals(afterIds))
            {
                return ContextPackageMigrationResult.Failure(
                    "package.migration.identity-changed",
                    "A schema migration changed one or more stable semantic identities.");
            }

            var report = new ContextPackageMigrationReport(
                sourceVersion,
                targetSchemaVersion,
                beforeDigest,
                loaded.Package.SemanticDigest,
                beforeIds.Order(StringComparer.Ordinal).ToImmutableArray(),
                steps.ToImmutable());
            return new ContextPackageMigrationResult(currentDocument, report, []);
        }
        catch (OperationCanceledException)
        {
            return ContextPackageMigrationResult.Failure("package.migration.cancelled", "Migration was cancelled.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return ContextPackageMigrationResult.Failure("package.migration.failed", exception.Message);
        }
    }

    private static HashSet<string> SemanticIds(JsonElement root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        CollectIds(root.GetProperty("context"), ids);
        CollectIds(root.GetProperty("definitions"), ids);
        return ids;
    }

    private static void CollectIds(JsonElement element, ISet<string> ids)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                ids.Add(id.GetString()!);
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectIds(property.Value, ids);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectIds(item, ids);
            }
        }
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

    private static ImmutableArray<ContextImport> ReadImports(JsonElement root) =>
        root.GetProperty("imports").EnumerateArray().Select(import => new ContextImport(
            import.GetProperty("contextId").GetString()!,
            import.GetProperty("versionRange").GetString()!,
            import.GetProperty("concepts").EnumerateArray().Select(concept => new ImportedConcept(
                concept.GetProperty("id").GetString()!,
                concept.GetProperty("kind").GetString()!)).ToImmutableArray())).ToImmutableArray();

    private static ImmutableArray<ExportedConcept> ReadExports(
        JsonElement root,
        AuthoredContextRevision revision) =>
        root.GetProperty("exports").EnumerateArray().Select(item =>
        {
            var id = item.GetString()!;
            var concept = revision.FindConcept(SemanticId.Parse(id)) ??
                throw new ArgumentException($"Exported concept '{id}' is not owned by this context package.");
            return new ExportedConcept(id, concept.Kind.ToString());
        }).ToImmutableArray();

    private static bool VersionRangeIncludes(string range, string version)
    {
        var candidate = Version.Parse(version);
        return range.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(constraint =>
        {
            if (constraint.StartsWith(">=", StringComparison.Ordinal))
            {
                return candidate >= Version.Parse(constraint[2..]);
            }

            if (constraint.StartsWith('<'))
            {
                return candidate < Version.Parse(constraint[1..]);
            }

            return candidate == Version.Parse(constraint.TrimStart('='));
        });
    }

    private static bool IsValidVersionRange(string range)
    {
        try
        {
            var constraints = range.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return constraints.Length is > 0 and <= 2 && constraints.All(constraint =>
            {
                var value = constraint.StartsWith(">=", StringComparison.Ordinal)
                    ? constraint[2..]
                    : constraint.StartsWith('<') || constraint.StartsWith('=')
                        ? constraint[1..]
                        : constraint;
                _ = Version.Parse(value);
                return constraint.StartsWith(">=", StringComparison.Ordinal) ||
                    constraint.StartsWith('<') ||
                    constraint.StartsWith('=') ||
                    constraints.Length == 1;
            });
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static SemanticDefinition ReadDefinition(JsonElement definition)
    {
        var identity = ReadIdentity(definition);
        return definition.GetProperty("kind").GetString() switch
        {
            "Entity" => new EntityDefinition(
                identity.Id,
                identity.Name,
                identity.Slug,
                ReadLifecycle(definition.GetProperty("lifecycle"))),
            "Fact" => new FactDefinition(
                identity.Id,
                identity.Name,
                identity.Slug,
                Enum.Parse<FactType>(definition.GetProperty("type").GetString()!)),
            "Rule" => new RuleDefinition(
                identity.Id,
                identity.Name,
                identity.Slug,
                definition.GetProperty("inputFacts").EnumerateArray()
                    .Select(item => new FactReference(SemanticId.Parse(item.GetString()!)))
                    .ToImmutableArray(),
                definition.GetProperty("conclusions").EnumerateArray()
                    .Select(ReadConclusion)
                    .ToImmutableArray()),
            "Behaviour" => ReadBehaviour(definition, identity),
            var kind => throw new ArgumentException($"Unsupported semantic definition kind '{kind}'.")
        };
    }

    private static BehaviourDefinition ReadBehaviour(JsonElement definition, Identity identity) =>
        new(
            identity.Id,
            identity.Name,
            identity.Slug,
            new EntityReference(SemanticId.Parse(definition.GetProperty("entity").GetString()!)),
            definition.GetProperty("outcomes").EnumerateArray().Select(ReadOutcome).ToImmutableArray(),
            [],
            [],
            definition.GetProperty("transitions").EnumerateArray().Select(ReadTransition).ToImmutableArray(),
            definition.GetProperty("ruleBindings").EnumerateArray().Select(ReadRuleBinding).ToImmutableArray());

    private static LifecycleDefinition ReadLifecycle(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new LifecycleDefinition(
            identity.Id,
            identity.Name,
            identity.Slug,
            element.GetProperty("stages").EnumerateArray()
                .Select(stage =>
                {
                    var stageIdentity = ReadIdentity(stage);
                    return new LifecycleStage(stageIdentity.Id, stageIdentity.Name, stageIdentity.Slug);
                })
                .ToImmutableArray());
    }

    private static ConclusionDefinition ReadConclusion(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new ConclusionDefinition(identity.Id, identity.Name, identity.Slug);
    }

    private static OutcomeDefinition ReadOutcome(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new OutcomeDefinition(identity.Id, identity.Name, identity.Slug);
    }

    private static TransitionDefinition ReadTransition(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new TransitionDefinition(
            identity.Id,
            identity.Name,
            identity.Slug,
            new LifecycleReference(SemanticId.Parse(element.GetProperty("lifecycle").GetString()!)),
            new LifecycleStageReference(SemanticId.Parse(element.GetProperty("sourceStage").GetString()!)),
            new LifecycleStageReference(SemanticId.Parse(element.GetProperty("targetStage").GetString()!)),
            new OutcomeReference(SemanticId.Parse(element.GetProperty("outcome").GetString()!)));
    }

    private static RuleBinding ReadRuleBinding(JsonElement element)
    {
        var bindings = element.GetProperty("factBindings").EnumerateObject().ToImmutableDictionary(
            property => new FactReference(SemanticId.Parse(property.Name)),
            property => new FactReference(SemanticId.Parse(property.Value.GetString()!)));
        return new RuleBinding(
            new RuleReference(SemanticId.Parse(element.GetProperty("rule").GetString()!)),
            Enum.Parse<RuleBindingPurpose>(element.GetProperty("purpose").GetString()!),
            bindings);
    }

    private static Identity ReadIdentity(JsonElement element) =>
        new(
            SemanticId.Parse(element.GetProperty("id").GetString()!),
            new SemanticName(element.GetProperty("name").GetString()!),
            new SemanticSlug(element.GetProperty("slug").GetString()!));

    private static byte[] Canonicalize(JsonElement element, bool semantic = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, element, null, semantic);
        }

        return stream.ToArray();
    }

    private static void WriteCanonical(
        Utf8JsonWriter writer,
        JsonElement element,
        string? propertyName,
        bool semantic)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .Where(property => !IsExcluded(property.Name, propertyName, semantic))
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value, property.Name, semantic);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in CanonicalItems(element, propertyName))
                {
                    WriteCanonical(writer, item, null, semantic);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static IEnumerable<JsonElement> CanonicalItems(JsonElement array, string? propertyName)
    {
        var items = array.EnumerateArray().ToArray();
        if (propertyName is not ("definitions" or "exports" or "inputFacts" or "stages" or
            "conclusions" or "outcomes" or "effects" or "publishedEvents" or "transitions" or "ruleBindings"))
        {
            return items;
        }

        return items.OrderBy(CanonicalArrayKey, StringComparer.Ordinal);
    }

    private static string CanonicalArrayKey(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            return item.GetString()!;
        }

        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var id))
        {
            return id.GetString()!;
        }

        return Encoding.UTF8.GetString(Canonicalize(item));
    }

    private static bool IsExcluded(string propertyName, string? containerName, bool semantic) =>
        propertyName is "layout" or "layoutState" or "provenance" or "sourceProvenance" or
            "packageDigest" or "semanticDigest" || semantic &&
            (propertyName is "schemaVersion" or "versionRange" ||
             containerName == "context" && propertyName == "version");

    private static string Digest(ReadOnlySpan<byte> value) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(value))}";

    private sealed record Identity(SemanticId Id, SemanticName Name, SemanticSlug Slug);
}

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
