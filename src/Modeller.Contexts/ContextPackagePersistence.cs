using System.Collections.Immutable;
using System.Text.Json;

namespace Modeller.Contexts;

public static partial class ContextPackageSystem
{
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
}
