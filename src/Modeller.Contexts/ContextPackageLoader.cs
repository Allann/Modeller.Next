using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Modeller.Model;

namespace Modeller.Contexts;

public static partial class ContextPackageSystem
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
            if (IsRooted(fragment.Name) ||
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

    private static bool IsRooted(string value) =>
        Path.IsPathRooted(value) || value.StartsWith('/') || value.StartsWith('\\') ||
        (value.Length >= 2 && value[1] == ':' && char.IsAsciiLetter(value[0]));

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
                    if (IsRooted(path) || segments.Contains("..", StringComparer.Ordinal))
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
}
