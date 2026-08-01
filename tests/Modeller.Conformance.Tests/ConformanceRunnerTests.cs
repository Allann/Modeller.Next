using System.Text.Json;
using Modeller.Conformance;
using Modeller.Model;
using Xunit;

namespace Modeller.Conformance.Tests;

public sealed class ConformanceRunnerTests
{
    [Fact]
    public async Task Independently_authored_observation_passes_through_public_adapter_seam()
    {
        var fixture = ConformanceFixture.Parse(
            """
            {
              "schemaVersion": "1.0",
              "scenarioId": "child-care.model.ownership.v1",
              "sourceDecision": "https://github.com/Allann/Modeller.Next/issues/16",
              "capability": "canonical-model",
              "contractVersion": "1.0",
              "inputDigest": "sha256:34967ee55f5482ad6dba7e84f4965a3696165f445ae5842f09edf6221e7e6cad",
              "input": { "context": "child-care" },
              "expected": {
                "context": "child-care",
                "conceptCount": 11
              }
            }
            """);
        var adapter = new StubAdapter(
            "canonical-model",
            "1.0",
            """{ "conceptCount": 11, "context": "child-care" }""");

        var report = await ConformanceRunner.RunAsync(fixture, adapter, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, report.Status);
        Assert.Equal("child-care.model.ownership.v1", report.ScenarioId);
        Assert.Empty(report.Mismatches);
    }

    [Fact]
    public async Task Mismatches_are_path_specific_and_permitted_operational_variance_is_ignored()
    {
        var fixture = ConformanceFixture.Parse(
            """
            {
              "schemaVersion": "1.0",
              "scenarioId": "child-care.accs.eligible.v1",
              "sourceDecision": "https://github.com/Allann/Modeller.Next/issues/17",
              "capability": "rule-evaluation",
              "contractVersion": "1.0",
              "inputDigest": "sha256:fd202bb1cbdee9a6fc3c795bdaca86f86bb7f0ed089803b3222053105f5951d0",
              "input": { "activeEnrolment": true },
              "expected": {
                "conclusion": "Eligible",
                "operational": { "durationMs": 0 }
              },
              "permittedVariance": ["/operational/durationMs"]
            }
            """);
        var adapter = new StubAdapter(
            "rule-evaluation",
            "1.0",
            """
            {
              "operational": { "durationMs": 55 },
              "conclusion": "Ineligible"
            }
            """);

        var report = await ConformanceRunner.RunAsync(fixture, adapter, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Mismatch, report.Status);
        var mismatch = Assert.Single(report.Mismatches);
        Assert.Equal("/conclusion", mismatch.Path);
    }

    [Fact]
    public void Fixture_catalog_discovers_scenarios_in_stable_order()
    {
        var second = Fixture("child-care.lifecycle.submit.v1", "lifecycle");
        var first = Fixture("child-care.model.ownership.v1", "canonical-model");

        var catalog = ConformanceFixtureCatalog.Load(
            [
                new ConformanceFixtureDocument("z-second.json", second),
                new ConformanceFixtureDocument("a-first.json", first)
            ]);

        Assert.Equal(
            ["child-care.lifecycle.submit.v1", "child-care.model.ownership.v1"],
            catalog.Fixtures.Select(fixture => fixture.ScenarioId));
    }

    [Fact]
    public void Evidence_catalog_maps_every_accepted_contract_to_planned_or_executable_evidence()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Conformance");
        var catalog = ConformanceEvidenceCatalog.Load(
            File.ReadAllText(Path.Combine(root, "diagnostic-catalogue.v1.json")),
            File.ReadAllText(Path.Combine(root, "coverage-manifest.v1.json")),
            File.ReadAllText(Path.Combine(root, "compatibility-matrix.v1.json")),
            File.ReadAllText(Path.Combine(root, "explanation-rubric.v1.json")),
            File.ReadAllText(Path.Combine(root, "security-threat-inventory.v1.json")));

        Assert.Equal(Enumerable.Range(16, 7), catalog.SourceDecisions);
        Assert.Contains("fixture.schema.unsupported", catalog.DiagnosticCodes);
        Assert.Contains("1.0", catalog.SupportedFixtureSchemas);
        Assert.False(catalog.SemanticWaiversPermitted);
        Assert.True(catalog.ImplementationThresholdReady);
    }

    [Fact]
    public async Task Semantic_mutation_check_proves_a_fixture_detects_changed_meaning()
    {
        var fixture = ConformanceFixture.Parse(Fixture("child-care.accs.mutation.v1", "rule-evaluation"));
        var baseline = new StubAdapter("rule-evaluation", "1.0", "{}");
        var mutant = new StubAdapter("rule-evaluation", "1.0", """{ "conclusion": "Ineligible" }""");

        var result = await SemanticMutationCheck.VerifyAsync(
            "change-eligible-conclusion",
            fixture,
            baseline,
            mutant,
            TestContext.Current.CancellationToken);

        Assert.Equal(SemanticMutationStatus.Killed, result.Status);
        Assert.Equal(ConformanceStatus.Mismatch, result.MutantReport.Status);
    }

    [Fact]
    public async Task Generated_failure_retains_generator_version_seed_and_minimized_repro()
    {
        var generator = new StubGenerator();
        var adapter = new StubAdapter("rule-evaluation", "1.0", """{ "unexpected": true }""");

        var result = await GeneratedConformanceRunner.RunAsync(
            generator,
            8675309,
            adapter,
            (fixture, _, _) => ValueTask.FromResult(fixture),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result.Failure);
        Assert.Equal("generator-v1", result.Failure.GeneratorVersion);
        Assert.Equal(8675309, result.Failure.Seed);
        Assert.Equal("generated.8675309", result.Failure.MinimizedFixture.ScenarioId);
    }

    [Fact]
    public async Task Adapter_contract_version_must_match_the_fixture()
    {
        var fixture = ConformanceFixture.Parse(Fixture("contract.version.v1", "canonical-model"));
        var adapter = new StubAdapter("canonical-model", "2.0", "{}");

        var report = await ConformanceRunner.RunAsync(fixture, adapter, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Invalid, report.Status);
        Assert.Equal("adapter.contract-version.mismatch", Assert.Single(report.Mismatches).Code);
    }

    [Fact]
    public void Fixture_schema_rejects_unknown_properties()
    {
        var json = Fixture("unknown.property.v1", "canonical-model")
            .Replace("\"expected\": {}", "\"expected\": {}, \"surprise\": true", StringComparison.Ordinal);

        var exception = Assert.Throws<ConformanceFixtureException>(() => ConformanceFixture.Parse(json));

        Assert.Equal("fixture.unknown-property", exception.Code);
    }

    [Fact]
    public void Semantic_variance_cannot_be_waived()
    {
        var json = Fixture("semantic.waiver.v1", "rule-evaluation")
            .Replace("\"expected\": {}", "\"expected\": {}, \"permittedVariance\": [\"/conclusion\"]", StringComparison.Ordinal);

        var exception = Assert.Throws<ConformanceFixtureException>(() => ConformanceFixture.Parse(json));

        Assert.Equal("fixture.semantic-variance-forbidden", exception.Code);
    }

    [Fact]
    public void Malformed_fixture_is_reported_with_a_stable_diagnostic()
    {
        var exception = Assert.Throws<ConformanceFixtureException>(() =>
            ConformanceFixture.Parse("{ not-json }"));

        Assert.Equal("fixture.malformed-json", exception.Code);
    }

    [Fact]
    public async Task Cancellation_and_adapter_failures_are_isolated_and_do_not_disclose_exceptions()
    {
        var fixture = ConformanceFixture.Parse(Fixture("failure.isolation.v1", "canonical-model"));
        var cancelled = new CancellationToken(canceled: true);

        var cancelledReport = await ConformanceRunner.RunAsync(
            fixture,
            new StubAdapter("canonical-model", "1.0", "{}"),
            cancelled);
        var failedReport = await ConformanceRunner.RunAsync(
            fixture,
            new ThrowingAdapter(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Cancelled, cancelledReport.Status);
        Assert.Equal(ConformanceStatus.Failed, failedReport.Status);
        var mismatch = Assert.Single(failedReport.Mismatches);
        Assert.Equal("adapter.failed", mismatch.Code);
        Assert.DoesNotContain("protected-child-id", mismatch.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_runs_return_the_same_request_local_report()
    {
        var fixture = ConformanceFixture.Parse(Fixture("concurrent.model.v1", "canonical-model"));
        var adapter = new StubAdapter("canonical-model", "1.0", "{}");

        var reports = await Task.WhenAll(Enumerable.Range(0, 32).Select(async _ =>
            await ConformanceRunner.RunAsync(fixture, adapter, TestContext.Current.CancellationToken)));

        Assert.All(reports, report =>
        {
            Assert.Equal(ConformanceStatus.Passed, report.Status);
            Assert.Empty(report.Mismatches);
        });
    }

    [Fact]
    public void Curated_accs_fixtures_are_discoverable_and_independently_versioned()
    {
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Conformance", "fixtures", "child-care");
        var documents = Directory.EnumerateFiles(fixtureRoot, "*.json")
            .Select(path => new ConformanceFixtureDocument(Path.GetFileName(path), File.ReadAllText(path)));

        var catalog = ConformanceFixtureCatalog.Load(documents);

        Assert.Equal(
            ["child-care.accs.eligible.v1", "child-care.accs.information-required.v1", "child-care.model.ownership.v1"],
            catalog.Fixtures.Select(fixture => fixture.ScenarioId));
        Assert.All(catalog.Fixtures, fixture => Assert.Empty(fixture.PermittedVariance));
    }

    [Fact]
    public async Task Canonical_model_adapter_conforms_through_its_public_interface()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Conformance",
            "fixtures",
            "child-care",
            "model-ownership.v1.json");
        var fixture = ConformanceFixture.Parse(File.ReadAllText(path));

        var report = await ConformanceRunner.RunAsync(
            fixture,
            new CanonicalModelAdapter(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, report.Status);
    }

    [Fact]
    public void Release_evidence_requires_passing_reports_and_killed_mutations()
    {
        var evidenceCatalog = new ConformanceEvidenceCatalog(
            ["fixture.schema.unsupported"],
            [16, 17, 18, 19, 20, 21, 22],
            ["1.0"],
            ["correctness"],
            ["protected-data-disclosure"],
            SemanticWaiversPermitted: false);
        var passed = new ConformanceReport("child-care.model.ownership.v1", ConformanceStatus.Passed, []);
        var killed = new SemanticMutationReport(
            "change-owner",
            SemanticMutationStatus.Killed,
            passed,
            new ConformanceReport(
                "child-care.model.ownership.v1",
                ConformanceStatus.Mismatch,
                [new ConformanceMismatch("/ownerId", "observation.value-mismatch", "Changed")]
            ));

        var release = ConformanceReleaseEvidence.Evaluate(evidenceCatalog, [passed], [killed], []);

        Assert.True(release.Ready);
        Assert.Empty(release.Blockers);
    }

    [Fact]
    public void Release_evidence_cannot_be_empty()
    {
        var evidenceCatalog = new ConformanceEvidenceCatalog(
            ["fixture.schema.unsupported"],
            [16, 17, 18, 19, 20, 21, 22],
            ["1.0"],
            ["correctness"],
            ["protected-data-disclosure"],
            SemanticWaiversPermitted: false);

        var release = ConformanceReleaseEvidence.Evaluate(evidenceCatalog, [], [], []);

        Assert.False(release.Ready);
        Assert.Contains(release.Blockers, blocker => blocker.Code == "release.conformance.missing");
        Assert.Contains(release.Blockers, blocker => blocker.Code == "release.mutation.missing");
    }

    [Fact]
    public async Task Historical_unknown_future_fixture_schema_is_rejected_safely()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Conformance",
            "compatibility",
            "unknown-future-fixture-schema.v1.json");
        using var compatibility = JsonDocument.Parse(File.ReadAllText(path));
        var fixture = ConformanceFixture.Parse(
            compatibility.RootElement.GetProperty("artifact").GetRawText());

        var report = await ConformanceRunner.RunAsync(
            fixture,
            new StubAdapter("canonical-model", "1.0", "{}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Invalid.ToString(), compatibility.RootElement.GetProperty("expected").GetProperty("status").GetString());
        Assert.Equal(
            compatibility.RootElement.GetProperty("expected").GetProperty("diagnosticCode").GetString(),
            Assert.Single(report.Mismatches).Code);
    }

    private static string Fixture(string scenarioId, string capability) => $$"""
        {
          "schemaVersion": "1.0",
          "scenarioId": "{{scenarioId}}",
          "sourceDecision": "https://github.com/Allann/Modeller.Next/issues/22",
          "capability": "{{capability}}",
          "contractVersion": "1.0",
          "inputDigest": "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
          "input": {},
          "expected": {}
        }
        """;

    private sealed class StubAdapter(
        string capability,
        string contractVersion,
        string observation) : IConformanceAdapter
    {
        public string Capability => capability;
        public string ContractVersion => contractVersion;

        public ValueTask<JsonElement> ExecuteAsync(
            JsonElement input,
            ConformanceExecutionContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(JsonDocument.Parse(observation).RootElement.Clone());
    }

    private sealed class StubGenerator : IConformanceFixtureGenerator
    {
        public string Version => "generator-v1";

        public ConformanceFixture Generate(long seed) =>
            ConformanceFixture.Parse(Fixture($"generated.{seed}", "rule-evaluation"));
    }

    private sealed class ThrowingAdapter : IConformanceAdapter
    {
        public string Capability => "canonical-model";
        public string ContractVersion => "1.0";

        public ValueTask<JsonElement> ExecuteAsync(
            JsonElement input,
            ConformanceExecutionContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("protected-child-id: 42");
    }

    private sealed class CanonicalModelAdapter : IConformanceAdapter
    {
        public string Capability => "canonical-model";
        public string ContractVersion => "1.0";

        public ValueTask<JsonElement> ExecuteAsync(
            JsonElement input,
            ConformanceExecutionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("child-care", input.GetProperty("boundedContext").GetString());
            var contextRevision = AuthoredContextRevision.Create(
                SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000001"),
                new SemanticName("Child Care"),
                new SemanticSlug("child-care"),
                "1.0.0");
            var entity = new EntityDefinition(
                SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000002"),
                new SemanticName("ACCS determination application"),
                new SemanticSlug("accs-determination-application"),
                new LifecycleDefinition(
                    SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000003"),
                    new SemanticName("Application lifecycle"),
                    new SemanticSlug("application-lifecycle"),
                    [
                        new LifecycleStage(
                            SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000004"),
                            new SemanticName("Draft"),
                            new SemanticSlug("draft")),
                        new LifecycleStage(
                            SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000005"),
                            new SemanticName("Submitted"),
                            new SemanticSlug("submitted"))
                    ]));
            var revision = CanonicalModel.Apply(contextRevision, new AddDefinition(entity)).Revision;
            var concepts = new[]
            {
                entity.Id,
                entity.Lifecycle.Id,
                entity.Lifecycle.Stages[0].Id,
                entity.Lifecycle.Stages[1].Id
            }.Select(id => revision.FindConcept(id)!).Select(concept => new
            {
                id = concept.Id.ToString(),
                kind = concept.Kind.ToString(),
                ownerId = concept.OwnerId.ToString(),
                qualifiedName = concept.QualifiedName
            });
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { concepts }));
        }
    }
}
