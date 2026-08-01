using Modeller.Contexts;
using Modeller.Conformance;
using Modeller.Validation;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Modeller.Validation.Tests;

public sealed class SemanticValidationTests
{
    [Fact]
    public void Complete_child_care_package_passes_every_validation_stage_without_mutation()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(document).Package!;
        var originalRevision = package.AuthoredRevision;
        var request = ValidationRequest.For(package, ValidationProfile.Canonical);

        var result = SemanticValidation.Validate(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            [
                ValidationStage.Decode,
                ValidationStage.IdentityAndOwnership,
                ValidationStage.ReferencesAndFederation,
                ValidationStage.TypesAndExpressions,
                ValidationStage.RulesAndDecisions,
                ValidationStage.Behaviours,
                ValidationStage.Lifecycles,
                ValidationStage.Views
            ],
            result.Stages.Select(stage => stage.Stage));
        Assert.All(result.Stages, stage => Assert.Equal(ValidationStageStatus.Passed, stage.Status));
        Assert.Same(originalRevision, package.AuthoredRevision);
    }

    [Fact]
    public void Unknown_eligibility_fact_produces_one_stable_located_diagnostic()
    {
        var root = JsonNode.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json")))!.AsObject();
        var rule = root["definitions"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(definition => definition["kind"]!.GetValue<string>() == "Rule");
        rule["inputFacts"]![0] = "0191f6d4-4ea0-7000-8000-00000000ffff";
        var package = ContextPackageSystem.Load(JsonSerializer.SerializeToUtf8Bytes(root)).Package!;

        var result = SemanticValidation.Validate(
            ValidationRequest.For(package, ValidationProfile.Canonical),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("validation.reference.fact-unresolved", diagnostic.Code);
        Assert.Equal(ValidationStage.ReferencesAndFederation, diagnostic.Stage);
        Assert.Equal("0191f6d4-4ea0-7000-8000-000000000008", diagnostic.SubjectId);
        Assert.Equal("/definitions/0191f6d4-4ea0-7000-8000-000000000008/inputFacts/0", diagnostic.Path);
        Assert.Equal(
            ValidationStageStatus.Failed,
            result.Stages.Single(stage => stage.Stage == ValidationStage.ReferencesAndFederation).Status);
        Assert.All(
            result.Stages.Where(stage => (int)stage.Stage > (int)ValidationStage.ReferencesAndFederation),
            stage => Assert.Equal(ValidationStageStatus.Skipped, stage.Status));
    }

    [Fact]
    public void Submission_transition_without_a_declared_outcome_is_rejected()
    {
        var root = JsonNode.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json")))!.AsObject();
        var behaviour = root["definitions"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(definition => definition["kind"]!.GetValue<string>() == "Behaviour");
        behaviour["transitions"]![0]!["outcome"] = "0191f6d4-4ea0-7000-8000-00000000ffff";
        var package = ContextPackageSystem.Load(JsonSerializer.SerializeToUtf8Bytes(root)).Package!;

        var result = SemanticValidation.Validate(
            ValidationRequest.For(package, ValidationProfile.Canonical),
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("validation.reference.outcome-unresolved", diagnostic.Code);
        Assert.Equal(ValidationStage.ReferencesAndFederation, diagnostic.Stage);
        Assert.Equal("0191f6d4-4ea0-7000-8000-00000000000d", diagnostic.SubjectId);
        Assert.Equal(
            "/definitions/0191f6d4-4ea0-7000-8000-00000000000a/transitions/0191f6d4-4ea0-7000-8000-00000000000d/outcome",
            diagnostic.Path);
    }

    [Fact]
    public void Cancellation_returns_no_partial_semantic_result()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(document).Package!;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = SemanticValidation.Validate(
            ValidationRequest.For(package, ValidationProfile.Canonical),
            cancellation.Token);

        Assert.True(result.IsCancelled);
        Assert.False(result.IsValid);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(ValidationStageStatus.Cancelled, result.Stages[0].Status);
        Assert.All(result.Stages.Skip(1), stage => Assert.Equal(ValidationStageStatus.Skipped, stage.Status));
    }

    [Fact]
    public void Versioned_extensions_are_ordered_and_fail_in_isolation_without_exposing_exceptions()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(document).Package!;
        IValidationExtension[] extensions =
        [
            new ThrowingExtension("z-failing", "secret child record"),
            new WarningExtension("a-child-care")
        ];

        var result = SemanticValidation.Validate(
            ValidationRequest.For(package, ValidationProfile.Canonical, extensions),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["extension.a-child-care.review", "validation.extension.failed"],
            result.Diagnostics.Select(diagnostic => diagnostic.Code));
        Assert.DoesNotContain("secret child record", string.Join(' ', result.Diagnostics.Select(item => item.Message)));
        Assert.Equal(
            ValidationStageStatus.Failed,
            result.Stages.Single(stage => stage.Stage == ValidationStage.Views).Status);
    }

    [Fact]
    public void Deterministic_work_budget_stops_at_the_same_semantic_stage()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(document).Package!;
        var restricted = new ValidationProfile("Restricted", 32, 1);

        var first = SemanticValidation.Validate(
            ValidationRequest.For(package, restricted),
            TestContext.Current.CancellationToken);
        var second = SemanticValidation.Validate(
            ValidationRequest.For(package, restricted),
            TestContext.Current.CancellationToken);

        Assert.Equal(first.Diagnostics.ToArray(), second.Diagnostics.ToArray());
        Assert.Equal(
            first.Stages.Select(stage => (stage.Stage, stage.Status)),
            second.Stages.Select(stage => (stage.Stage, stage.Status)));
        var diagnostic = Assert.Single(first.Diagnostics);
        Assert.Equal("validation.limit.work-exceeded", diagnostic.Code);
        Assert.Equal(ValidationStage.IdentityAndOwnership, diagnostic.Stage);
    }

    [Fact]
    public void Authored_context_skips_decode_and_runs_every_semantic_stage()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var revision = ContextPackageSystem.Load(document).Package!.AuthoredRevision;

        var result = SemanticValidation.Validate(
            ValidationRequest.For(revision, ValidationProfile.Canonical),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal(ValidationStageStatus.Skipped, result.Stages[0].Status);
        Assert.All(result.Stages.Skip(1), stage => Assert.Equal(ValidationStageStatus.Passed, stage.Status));
    }

    [Fact]
    public void Resolved_snapshot_validates_its_exact_package_lock_and_semantics()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(document).Package!;
        var resolution = ContextPackageSystem.Resolve(
            [document],
            new ContextPackageIdentity(package.AuthoredRevision.Id.ToString(), package.AuthoredRevision.ContextVersion),
            TestContext.Current.CancellationToken);

        var result = SemanticValidation.Validate(
            ValidationRequest.For(
                resolution.Snapshot!,
                [package],
                ValidationProfile.Canonical),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.All(result.Stages, stage => Assert.Equal(ValidationStageStatus.Passed, stage.Status));
    }

    [Fact]
    public async Task Unknown_fact_diagnostic_passes_executable_conformance_evidence()
    {
        var fixture = ConformanceFixture.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "validation-unknown-fact.v1.json")));

        var report = await ConformanceRunner.RunAsync(
            fixture,
            new ValidationConformanceAdapter(Path.Combine(AppContext.BaseDirectory, "Fixtures")),
            TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, report.Status);
        Assert.Empty(report.Mismatches);
    }

    [Fact]
    public void Validation_profile_cannot_hide_errors_with_zero_limits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValidationProfile("Invalid", 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValidationProfile("Invalid", 100, 0));
    }

    [Fact]
    public void Diagnostic_limit_reports_overflow_instead_of_turning_failure_into_success()
    {
        var document = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(document).Package!;
        IValidationExtension[] extensions =
        [new WarningExtension("a-child-care"), new ThrowingExtension("z-failing", "secret")];

        var result = SemanticValidation.Validate(
            ValidationRequest.For(package, new ValidationProfile("Restricted", 1, 100), extensions),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal("validation.limit.diagnostics-exceeded", Assert.Single(result.Diagnostics).Code);
    }

    private sealed class WarningExtension(string id) : IValidationExtension
    {
        public string Id => id;
        public string Version => "1.0";
        public ValidationStage Stage => ValidationStage.Views;

        public IEnumerable<ValidationDiagnostic> Validate(
            ValidationExtensionContext context,
            CancellationToken cancellationToken) =>
            [new ValidationDiagnostic(
                $"extension.{Id}.review",
                Stage,
                ValidationSeverity.Warning,
                "A Child Care author review is recommended.")];
    }

    private sealed class ThrowingExtension(string id, string secret) : IValidationExtension
    {
        public string Id => id;
        public string Version => "1.0";
        public ValidationStage Stage => ValidationStage.Views;

        public IEnumerable<ValidationDiagnostic> Validate(
            ValidationExtensionContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(secret);
    }

    private sealed class ValidationConformanceAdapter(string fixtureDirectory) : IConformanceAdapter
    {
        public string Capability => "semantic-validation";
        public string ContractVersion => "1.0";

        public ValueTask<JsonElement> ExecuteAsync(
            JsonElement input,
            ConformanceExecutionContext context,
            CancellationToken cancellationToken)
        {
            var root = JsonNode.Parse(File.ReadAllText(Path.Combine(
                fixtureDirectory,
                input.GetProperty("artifact").GetString()!)))!.AsObject();
            var rule = root["definitions"]!.AsArray()
                .Select(item => item!.AsObject())
                .Single(definition => definition["kind"]!.GetValue<string>() == "Rule");
            rule["inputFacts"]![0] = "0191f6d4-4ea0-7000-8000-00000000ffff";
            var package = ContextPackageSystem.Load(JsonSerializer.SerializeToUtf8Bytes(root)).Package!;
            var result = SemanticValidation.Validate(
                ValidationRequest.For(package, ValidationProfile.Canonical),
                cancellationToken);
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new
            {
                isValid = result.IsValid,
                diagnostics = result.Diagnostics.Select(diagnostic => new
                {
                    code = diagnostic.Code,
                    stage = diagnostic.Stage.ToString(),
                    severity = diagnostic.Severity.ToString(),
                    subjectId = diagnostic.SubjectId,
                    path = diagnostic.Path
                })
            }));
        }
    }
}
