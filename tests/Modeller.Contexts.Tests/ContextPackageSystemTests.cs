using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Modeller.Conformance;
using Modeller.Contexts;
using Xunit;

namespace Modeller.Contexts.Tests;

public sealed class ContextPackageSystemTests
{
    [Fact]
    public void Child_care_package_loads_as_normalized_authored_meaning()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));

        var result = ContextPackageSystem.Load(document);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Package);
        Assert.Equal("Child Care", result.Package.AuthoredRevision.Name.Value);
        Assert.Equal("1.0.0", result.Package.AuthoredRevision.ContextVersion);
        Assert.Equal(5, result.Package.AuthoredRevision.Definitions.Length);
        Assert.Equal(
            "sha256:26b35b94c741cae8ffb8aafac1ad7cefb7bb5bf106cddc5db27544f5ccdfcd16",
            result.Package.SemanticDigest);
    }

    [Fact]
    public void Packaging_layout_and_provenance_do_not_change_semantic_digest()
    {
        var original = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var root = JsonNode.Parse(original)!.AsObject();
        root["layout"] = JsonNode.Parse("""{ "x": 100, "y": 200 }""");
        root["provenance"] = JsonNode.Parse("""{ "source": "moved/accs.modeller" }""");
        var definitions = root["definitions"]!.AsArray();
        root["definitions"] = new JsonArray(definitions.Reverse().Select(item => item!.DeepClone()).ToArray());
        var repackaged = JsonSerializer.SerializeToUtf8Bytes(
            root,
            new JsonSerializerOptions { WriteIndented = true });

        var baseline = ContextPackageSystem.Load(Encoding.UTF8.GetBytes(original));
        var result = ContextPackageSystem.Load(repackaged);

        Assert.True(result.IsSuccess);
        Assert.Equal(baseline.Package!.SemanticDigest, result.Package!.SemanticDigest);
        Assert.NotEqual(baseline.Package.PackageDigest, result.Package.PackageDigest);
    }

    [Fact]
    public void Context_patch_version_does_not_change_semantic_digest()
    {
        var original = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var patched = original.Replace("\"version\": \"1.0.0\"", "\"version\": \"1.0.1\"", StringComparison.Ordinal);

        var baseline = ContextPackageSystem.Load(Encoding.UTF8.GetBytes(original));
        var result = ContextPackageSystem.Load(Encoding.UTF8.GetBytes(patched));

        Assert.True(result.IsSuccess);
        Assert.Equal(baseline.Package!.SemanticDigest, result.Package!.SemanticDigest);
        Assert.NotEqual(baseline.Package.PackageDigest, result.Package.PackageDigest);
    }

    [Fact]
    public void Source_partitioning_and_document_names_do_not_change_semantic_digest()
    {
        var original = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var first = JsonNode.Parse(original)!.AsObject();
        var second = first.DeepClone().AsObject();
        var definitions = first["definitions"]!.AsArray().Select(item => item!.DeepClone()).ToArray();
        first["definitions"] = new JsonArray(definitions.Take(2).ToArray());
        second["definitions"] = new JsonArray(definitions.Skip(2).ToArray());

        var baseline = ContextPackageSystem.Load(Encoding.UTF8.GetBytes(original));
        var result = ContextPackageSystem.Load(
            [
                new ContextPackageDocument("rules/accs.json", JsonSerializer.SerializeToUtf8Bytes(second)),
                new ContextPackageDocument("model/entities.json", JsonSerializer.SerializeToUtf8Bytes(first))
            ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Package!.AuthoredRevision.Definitions.Length);
        Assert.Equal(baseline.Package!.SemanticDigest, result.Package.SemanticDigest);
        Assert.NotEqual(baseline.Package.PackageDigest, result.Package.PackageDigest);
    }

    [Fact]
    public void Duplicate_definition_across_package_fragments_is_rejected()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));

        var result = ContextPackageSystem.Load(
            [new ContextPackageDocument("first.json", document), new ContextPackageDocument("second.json", document)]);

        Assert.False(result.IsSuccess);
        Assert.Equal("package.identity.duplicate", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Child_care_package_resolves_to_an_exact_immutable_snapshot_lock()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));

        var result = ContextPackageSystem.Resolve(
            [document],
            new ContextPackageIdentity(
                "0191f6d4-4ea0-7000-8000-000000000001",
                "1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var lockedPackage = Assert.Single(result.Snapshot!.Packages);
        Assert.Equal("child-care", lockedPackage.ContextSlug);
        Assert.Equal("1.0.0", lockedPackage.ContextVersion);
        Assert.Equal(
            "sha256:26b35b94c741cae8ffb8aafac1ad7cefb7bb5bf106cddc5db27544f5ccdfcd16",
            lockedPackage.SemanticDigest);
        Assert.Equal(result.Snapshot.Packages, result.Snapshot.Packages.OrderBy(item => item.ContextId));
    }

    [Fact]
    public void Explicit_import_resolves_against_a_matching_public_export()
    {
        const string provider = """
            {
              "schemaVersion":"1.0", "languageVersion":"1.0",
              "context":{"id":"0191f6d4-4ea0-7000-8000-000000000101","name":"Child Care Provider","slug":"child-care-provider","version":"1.2.0"},
              "imports":[],
              "exports":["0191f6d4-4ea0-7000-8000-000000000102"],
              "definitions":[{"kind":"Fact","id":"0191f6d4-4ea0-7000-8000-000000000102","name":"Provider is approved","slug":"provider-is-approved","type":"Truth"}]
            }
            """;
        const string consumer = """
            {
              "schemaVersion":"1.0", "languageVersion":"1.0",
              "context":{"id":"0191f6d4-4ea0-7000-8000-000000000201","name":"Child Care","slug":"child-care","version":"2.0.0"},
              "imports":[{
                "contextId":"0191f6d4-4ea0-7000-8000-000000000101",
                "versionRange":">=1.0.0 <2.0.0",
                "concepts":[{"id":"0191f6d4-4ea0-7000-8000-000000000102","kind":"Fact"}]
              }],
              "exports":[],
              "definitions":[{"kind":"Fact","id":"0191f6d4-4ea0-7000-8000-000000000202","name":"Active enrolment exists","slug":"active-enrolment-exists","type":"Truth"}]
            }
            """;

        var result = ContextPackageSystem.Resolve(
            [Encoding.UTF8.GetBytes(consumer), Encoding.UTF8.GetBytes(provider)],
            new ContextPackageIdentity("0191f6d4-4ea0-7000-8000-000000000201", "2.0.0"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Snapshot!.Packages.Length);
        Assert.Equal(
            ["child-care-provider", "child-care"],
            result.Snapshot.Packages.Select(item => item.ContextSlug));
    }

    [Fact]
    public void Unknown_future_schema_is_rejected_with_a_stable_diagnostic()
    {
        var document = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"))
            .Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"99.0\"", StringComparison.Ordinal);

        var result = ContextPackageSystem.Load(Encoding.UTF8.GetBytes(document));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Package);
        Assert.Equal("package.schema.unsupported", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Historical_future_persistence_schema_fixture_remains_safely_rejected()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "unknown-future-persistence-schema.v1.json")));

        var result = ContextPackageSystem.Load(
            Encoding.UTF8.GetBytes(fixture.RootElement.GetProperty("package").GetRawText()));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            fixture.RootElement.GetProperty("expectedDiagnostic").GetString(),
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Import_cycle_is_rejected_instead_of_becoming_a_snapshot()
    {
        var first = PackageWithImport(
            "0191f6d4-4ea0-7000-8000-000000000301",
            "first",
            "0191f6d4-4ea0-7000-8000-000000000302",
            "0191f6d4-4ea0-7000-8000-000000000401",
            "0191f6d4-4ea0-7000-8000-000000000402");
        var second = PackageWithImport(
            "0191f6d4-4ea0-7000-8000-000000000401",
            "second",
            "0191f6d4-4ea0-7000-8000-000000000402",
            "0191f6d4-4ea0-7000-8000-000000000301",
            "0191f6d4-4ea0-7000-8000-000000000302");

        var result = ContextPackageSystem.Resolve(
            [Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second)],
            new ContextPackageIdentity("0191f6d4-4ea0-7000-8000-000000000301", "1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("package.import.cycle", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Persist_writes_one_deterministic_canonical_package()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var loaded = ContextPackageSystem.Load(document).Package!;

        var first = ContextPackageSystem.Persist(loaded);
        var second = ContextPackageSystem.Persist(loaded);
        var reloaded = ContextPackageSystem.Load(first.Document);

        Assert.Equal(first.Document.ToArray(), second.Document.ToArray());
        Assert.Equal(first.PackageDigest, second.PackageDigest);
        Assert.Equal(loaded.SemanticDigest, reloaded.Package!.SemanticDigest);
        Assert.NotEqual(document, first.Document.ToArray());
    }

    [Fact]
    public void Explicit_schema_migration_preserves_meaning_and_the_original_document()
    {
        var current = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var original = Encoding.UTF8.GetBytes(
            current.Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"0.9\"", StringComparison.Ordinal));
        var retainedOriginal = original.ToArray();

        var result = ContextPackageSystem.Migrate(
            original,
            "1.0",
            [new SchemaVersionReplacementMigration("0.9", "1.0")],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(retainedOriginal, original);
        Assert.Equal("0.9", result.Report!.SourceSchemaVersion);
        Assert.Equal("1.0", result.Report.TargetSchemaVersion);
        Assert.Equal(result.Report.BeforeSemanticDigest, result.Report.AfterSemanticDigest);
        Assert.Equal("1.0", ContextPackageSystem.Load(result.Document).Package!.SchemaVersion);
    }

    [Fact]
    public void Excessive_package_size_is_rejected_before_json_decode()
    {
        var oversized = new byte[5 * 1024 * 1024 + 1];

        var result = ContextPackageSystem.Load(oversized);

        Assert.False(result.IsSuccess);
        Assert.Equal("package.limit.size", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Duplicate_json_property_is_rejected_deterministically()
    {
        var original = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var duplicate = original.Replace(
            "\"schemaVersion\": \"1.0\",",
            "\"schemaVersion\": \"1.0\", \"schemaVersion\": \"1.0\",",
            StringComparison.Ordinal);

        var result = ContextPackageSystem.Load(Encoding.UTF8.GetBytes(duplicate));

        Assert.False(result.IsSuccess);
        Assert.Equal("package.property.duplicate", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Published_context_version_cannot_resolve_to_two_different_package_digests()
    {
        var original = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var changed = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(original).Replace("Child Care", "Changed Child Care", StringComparison.Ordinal));

        var result = ContextPackageSystem.Resolve(
            [original, changed],
            new ContextPackageIdentity("0191f6d4-4ea0-7000-8000-000000000001", "1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("package.version.mutable", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Stable_identity_collision_is_a_structured_package_diagnostic()
    {
        var original = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var collision = original.Replace(
            "\"id\": \"0191f6d4-4ea0-7000-8000-000000000007\"",
            "\"id\": \"0191f6d4-4ea0-7000-8000-000000000006\"",
            StringComparison.Ordinal);

        var result = ContextPackageSystem.Load(Encoding.UTF8.GetBytes(collision));

        Assert.False(result.IsSuccess);
        Assert.Equal("package.identity.duplicate", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Dynamic_latest_dependency_selection_is_rejected()
    {
        var provider = PackageWithImport(
                "0191f6d4-4ea0-7000-8000-000000000301",
                "first",
                "0191f6d4-4ea0-7000-8000-000000000302",
                "0191f6d4-4ea0-7000-8000-000000000401",
                "0191f6d4-4ea0-7000-8000-000000000402")
            .Replace("=1.0.0", "latest", StringComparison.Ordinal);
        var dependency = PackageWithImport(
            "0191f6d4-4ea0-7000-8000-000000000401",
            "second",
            "0191f6d4-4ea0-7000-8000-000000000402",
            "0191f6d4-4ea0-7000-8000-000000000501",
            "0191f6d4-4ea0-7000-8000-000000000502");

        var result = ContextPackageSystem.Resolve(
            [Encoding.UTF8.GetBytes(provider), Encoding.UTF8.GetBytes(dependency)],
            new ContextPackageIdentity("0191f6d4-4ea0-7000-8000-000000000301", "1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("package.import.version-range-invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Supplied_but_incompatible_dependency_is_distinguished_from_missing()
    {
        var consumer = PackageWithImport(
                "0191f6d4-4ea0-7000-8000-000000000301",
                "first",
                "0191f6d4-4ea0-7000-8000-000000000302",
                "0191f6d4-4ea0-7000-8000-000000000401",
                "0191f6d4-4ea0-7000-8000-000000000402")
            .Replace("=1.0.0", ">=2.0.0 <3.0.0", StringComparison.Ordinal);
        var dependency = PackageWithImport(
            "0191f6d4-4ea0-7000-8000-000000000401",
            "second",
            "0191f6d4-4ea0-7000-8000-000000000402",
            "0191f6d4-4ea0-7000-8000-000000000501",
            "0191f6d4-4ea0-7000-8000-000000000502");

        var result = ContextPackageSystem.Resolve(
            [Encoding.UTF8.GetBytes(consumer), Encoding.UTF8.GetBytes(dependency)],
            new ContextPackageIdentity("0191f6d4-4ea0-7000-8000-000000000301", "1.0.0"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("package.import.incompatible", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Declared_semantic_digest_mismatch_is_rejected()
    {
        var original = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var root = JsonNode.Parse(original)!.AsObject();
        root["semanticDigest"] = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        var result = ContextPackageSystem.Load(JsonSerializer.SerializeToUtf8Bytes(root));

        Assert.False(result.IsSuccess);
        Assert.Equal("package.digest.semantic-mismatch", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Traversal_path_in_package_metadata_is_rejected()
    {
        var original = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var root = JsonNode.Parse(original)!.AsObject();
        root["provenance"] = JsonNode.Parse("""{ "sourcePath": "../../private.json" }""");

        var result = ContextPackageSystem.Load(JsonSerializer.SerializeToUtf8Bytes(root));

        Assert.False(result.IsSuccess);
        Assert.Equal("package.path.invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task Child_care_package_resolution_passes_executable_conformance_evidence()
    {
        var fixture = ConformanceFixture.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "context-package-resolution.v1.json")));
        var adapter = new ContextPackageConformanceAdapter(
            Path.Combine(AppContext.BaseDirectory, "Fixtures"));

        var report = await ConformanceRunner.RunAsync(
            fixture,
            adapter,
            TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, report.Status);
        Assert.Empty(report.Mismatches);
    }

    private static string PackageWithImport(
        string contextId,
        string slug,
        string factId,
        string importedContextId,
        string importedFactId) => $$"""
        {
          "schemaVersion":"1.0", "languageVersion":"1.0",
          "context":{"id":"{{contextId}}","name":"{{slug}}","slug":"{{slug}}","version":"1.0.0"},
          "imports":[{"contextId":"{{importedContextId}}","versionRange":"=1.0.0","concepts":[{"id":"{{importedFactId}}","kind":"Fact"}]}],
          "exports":["{{factId}}"],
          "definitions":[{"kind":"Fact","id":"{{factId}}","name":"{{slug}} fact","slug":"{{slug}}-fact","type":"Truth"}]
        }
        """;

    private sealed class SchemaVersionReplacementMigration(string source, string target)
        : IContextPackageMigration
    {
        public string SourceSchemaVersion => source;
        public string TargetSchemaVersion => target;

        public ReadOnlyMemory<byte> Transform(
            ReadOnlyMemory<byte> sourceDocument,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(sourceDocument.Span)
                    .Replace($"\"schemaVersion\": \"{source}\"", $"\"schemaVersion\": \"{target}\"", StringComparison.Ordinal));
        }
    }

    private sealed class ContextPackageConformanceAdapter(string fixtureDirectory) : IConformanceAdapter
    {
        public string Capability => "context-package-resolution";
        public string ContractVersion => "1.0";

        public ValueTask<JsonElement> ExecuteAsync(
            JsonElement input,
            ConformanceExecutionContext context,
            CancellationToken cancellationToken)
        {
            var document = File.ReadAllBytes(Path.Combine(
                fixtureDirectory,
                input.GetProperty("artifact").GetString()!));
            var result = ContextPackageSystem.Resolve(
                [document],
                new ContextPackageIdentity(
                    input.GetProperty("contextId").GetString()!,
                    input.GetProperty("contextVersion").GetString()!),
                cancellationToken);
            var observation = JsonSerializer.SerializeToElement(new
            {
                semanticDigest = result.Snapshot!.Packages[0].SemanticDigest,
                packages = result.Snapshot.Packages.Select(package => new
                {
                    contextId = package.ContextId,
                    contextSlug = package.ContextSlug,
                    contextVersion = package.ContextVersion,
                    packageDigest = package.PackageDigest
                })
            });
            return ValueTask.FromResult(observation);
        }
    }
}
