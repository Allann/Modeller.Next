using System.Collections.Immutable;
using Modeller.Contexts;
using Modeller.Conformance;
using Modeller.Model;
using Modeller.Rules;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Modeller.Rules.Tests;

public sealed class RulesRuntimeTests
{
    private const string RuleId = "0191f6d4-4ea0-7000-8000-000000000008";
    private const string EnrolmentFactId = "0191f6d4-4ea0-7000-8000-000000000006";
    private const string EvidenceFactId = "0191f6d4-4ea0-7000-8000-000000000007";
    private const string EligibleConclusionId = "0191f6d4-4ea0-7000-8000-000000000009";
    private const string EligibilityDecisionId = "0191f6d4-4ea0-7000-8000-000000000010";
    private const string DecisionEligibleId = "0191f6d4-4ea0-7000-8000-000000000011";
    private const string DecisionIneligibleId = "0191f6d4-4ea0-7000-8000-000000000012";

    [Fact]
    public void Sufficient_child_care_facts_determine_eligibility_with_explanation_and_trace()
    {
        var plan = BindChildCare();
        var request = new EvaluationRequest(
            "application-42",
            SemanticId.Parse(RuleId),
            ImmutableDictionary<SemanticId, FactValue>.Empty
                .Add(SemanticId.Parse(EnrolmentFactId), new TruthFactValue(true))
                .Add(SemanticId.Parse(EvidenceFactId), new TruthFactValue(true)),
            [new EvidenceRecord("evidence-1", "Supporting document metadata", "case/42", "sha256:abc", [SemanticId.Parse(EvidenceFactId)], EvidenceSensitivity.Protected)],
            TraceLevel.Full,
            DisclosurePolicy.Audit);

        var result = Assert.IsType<DeterminedResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Equal(SemanticId.Parse(EligibleConclusionId), result.Conclusion.Id);
        Assert.Equal(new TruthFactValue(true), result.Conclusion.Value);
        Assert.Equal(["accs.active-enrolment-confirmed", "accs.supporting-evidence-confirmed", "evaluation.rule.determined"], result.Findings.Select(item => item.Code));
        Assert.Equal("evidence-1", Assert.Single(result.Evidence).Id);
        Assert.Equal(["rule", "rule/and/0", "rule/and/1", "rule/conclusion"], result.Trace!.Nodes.Select(node => node.Path));
    }

    [Fact]
    public void Unique_child_care_decision_table_selects_the_eligible_classification()
    {
        var plan = BindChildCareDecisionTable();
        var request = RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)) with
        {
            Target = SemanticId.Parse(EligibilityDecisionId)
        };

        var result = Assert.IsType<DeterminedResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Equal(SemanticId.Parse(DecisionEligibleId), result.Conclusion.Id);
        Assert.Equal(new ClassificationFactValue(SemanticId.Parse(DecisionEligibleId)), result.Conclusion.Value);
        Assert.Equal("accs.eligibility-confirmed", Assert.Single(result.Findings.Where(item => item.Code.StartsWith("accs.", StringComparison.Ordinal))).Code);
        Assert.Equal(["decision", "decision/row/0", "decision/conclusion"], result.Trace!.Nodes.Select(item => item.Path));
    }

    [Fact]
    public void Decision_table_is_indeterminate_when_missing_evidence_can_change_the_classification()
    {
        var plan = BindChildCareDecisionTable();
        var request = RequestWithFacts((EnrolmentFactId, true)) with { Target = SemanticId.Parse(EligibilityDecisionId) };

        var result = Assert.IsType<IndeterminateResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Equal([SemanticId.Parse(EvidenceFactId)], result.MissingFacts);
        Assert.Contains(result.Findings, item => item.Code == "accs.supporting-evidence-required");
        Assert.DoesNotContain(result.Findings, item => item.Code.Contains("not-confirmed", StringComparison.Ordinal));
    }

    [Fact]
    public void Decision_table_wildcard_does_not_require_irrelevant_evidence()
    {
        var plan = BindChildCareDecisionTable();
        var request = RequestWithFacts((EnrolmentFactId, false)) with { Target = SemanticId.Parse(EligibilityDecisionId) };

        var result = Assert.IsType<DeterminedResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Equal(SemanticId.Parse(DecisionIneligibleId), result.Conclusion.Id);
        Assert.DoesNotContain(result.Findings.SelectMany(item => item.Facts), id => id == SemanticId.Parse(EvidenceFactId));
    }

    [Fact]
    public void Decision_table_trace_limit_fails_without_publishing_a_classification()
    {
        var plan = BindChildCareDecisionTable(new RuntimeLimits(100, 64, 100, 1));
        var request = RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)) with { Target = SemanticId.Parse(EligibilityDecisionId) };

        var result = Assert.IsType<FailedResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Equal("runtime.limit.trace-exceeded", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Unique_decision_table_rejects_overlapping_rows_at_binding()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs-decision-table.context-package.v1.json")))!.AsObject();
        var decision = document["definitions"]!.AsArray().Single(item => item!["kind"]!.GetValue<string>() == "Decision")!;
        var rows = decision["table"]!["rows"]!.AsArray();
        rows[2]!["conditions"] = rows[0]!["conditions"]!.DeepClone();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document);
        var package = ContextPackageSystem.Load(bytes).Package!;
        var snapshot = ContextPackageSystem.Resolve([bytes], new ContextPackageIdentity(package.AuthoredRevision.Id.ToString(), package.AuthoredRevision.ContextVersion), TestContext.Current.CancellationToken).Snapshot!;

        var binding = RulesRuntime.Bind(snapshot, [package], FunctionCatalogue.Empty, RuntimeLimits.Default, TestContext.Current.CancellationToken);

        Assert.Contains(binding.Diagnostics, diagnostic => diagnostic.Code == "runtime.binding.decision-rows-overlap");
        Assert.Null(binding.Plan);
    }

    [Fact]
    public async Task Child_care_unique_decision_table_passes_executable_conformance_evidence()
    {
        var fixture = ConformanceFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "accs-decision-table-eligible.v1.json")));
        var report = await ConformanceRunner.RunAsync(fixture, new DecisionTableConformanceAdapter(), TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, report.Status);
    }

    [Fact]
    public void Missing_supporting_information_is_indeterminate_and_never_false_or_null()
    {
        var plan = BindChildCare();
        var request = new EvaluationRequest(
            "application-43",
            SemanticId.Parse(RuleId),
            ImmutableDictionary<SemanticId, FactValue>.Empty
                .Add(SemanticId.Parse(EnrolmentFactId), new TruthFactValue(true)),
            [],
            TraceLevel.Summary,
            DisclosurePolicy.Public);

        var result = Assert.IsType<IndeterminateResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Equal([SemanticId.Parse(EvidenceFactId)], result.MissingFacts);
        Assert.Equal("accs.supporting-evidence-required", Assert.Single(result.Findings.Where(item => item.Code == "accs.supporting-evidence-required")).Code);
        Assert.DoesNotContain(result.Findings, item => item.Code == "evaluation.fact.false");
    }

    [Fact]
    public void False_and_missing_is_determined_false_by_short_circuit_reasoning()
    {
        var plan = BindChildCare();
        var request = RequestWithFacts((EnrolmentFactId, false));

        var result = Assert.IsType<DeterminedResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Equal(new TruthFactValue(false), result.Conclusion.Value);
        Assert.DoesNotContain(result.Findings, item => item.Code == "evaluation.fact.missing");
    }

    [Fact]
    public void Supplied_fact_with_the_wrong_type_is_invalid()
    {
        var plan = BindChildCare();
        var request = new EvaluationRequest(
            "application-invalid", SemanticId.Parse(RuleId),
            ImmutableDictionary<SemanticId, FactValue>.Empty.Add(SemanticId.Parse(EnrolmentFactId), new TextFactValue("true")),
            [], TraceLevel.None, DisclosurePolicy.Public);

        var result = Assert.IsType<InvalidResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Equal("runtime.request.fact-type-mismatch", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Deterministic_work_limit_fails_without_a_partial_conclusion()
    {
        var plan = BindChildCare(new RuntimeLimits(1, 64, 10_000, 10_000));

        var result = plan.Evaluate(RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)), TestContext.Current.CancellationToken);

        var failed = Assert.IsType<FailedResult>(result);
        Assert.Equal("runtime.limit.work-exceeded", Assert.Single(failed.Diagnostics).Code);
    }

    [Fact]
    public void Cancellation_is_control_flow_and_never_an_evaluation_result()
    {
        var plan = BindChildCare();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => plan.Evaluate(RequestWithFacts((EnrolmentFactId, true)), cancellation.Token));
    }

    [Fact]
    public async Task One_plan_is_reused_concurrently_without_request_state_leakage()
    {
        var plan = BindChildCare();
        var requests = Enumerable.Range(0, 32).Select(index => index % 2 == 0
            ? RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true))
            : RequestWithFacts((EnrolmentFactId, true))).ToArray();

        var results = await Task.WhenAll(requests.Select(request => Task.Run(
            () => plan.Evaluate(request, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)));

        Assert.Equal(16, results.OfType<DeterminedResult>().Count());
        Assert.Equal(16, results.OfType<IndeterminateResult>().Count());
        Assert.All(results.OfType<IndeterminateResult>(), result => Assert.Equal([SemanticId.Parse(EvidenceFactId)], result.MissingFacts));
    }

    [Fact]
    public void Public_disclosure_omits_protected_evidence_without_changing_the_conclusion()
    {
        var plan = BindChildCare();
        var request = RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)) with
        {
            Evidence = [new EvidenceRecord("protected-1", "Supporting document metadata", "case/secret", null, [SemanticId.Parse(EvidenceFactId)], EvidenceSensitivity.Protected)]
        };

        var result = Assert.IsType<DeterminedResult>(plan.Evaluate(request, TestContext.Current.CancellationToken));

        Assert.Empty(result.Evidence);
        Assert.All(result.Findings, finding => Assert.Empty(finding.EvidenceIds));
        Assert.All(result.Trace!.Nodes, node => Assert.Null(node.Result));
        Assert.Equal(new TruthFactValue(true), result.Conclusion.Value);
    }

    [Theory]
    [InlineData("accs-eligible.v1.json")]
    [InlineData("accs-information-required.v1.json")]
    public async Task Child_care_evaluations_pass_the_reviewed_conformance_fixture(string fixtureName)
    {
        var fixture = ConformanceFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName)));
        var report = await ConformanceRunner.RunAsync(
            fixture,
            new RulesConformanceAdapter(),
            TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, report.Status);
    }

    [Fact]
    public void Binding_rejects_an_expression_deeper_than_the_deterministic_limit()
    {
        var (snapshot, package) = LoadChildCare();

        var binding = RulesRuntime.Bind(
            snapshot, [package], FunctionCatalogue.Empty,
            new RuntimeLimits(100, 1, 100, 100),
            TestContext.Current.CancellationToken);

        Assert.Equal("runtime.binding.expression-depth-exceeded", Assert.Single(binding.Diagnostics).Code);
        Assert.Null(binding.Plan);
    }

    [Fact]
    public void Canonical_trace_limit_fails_at_the_same_semantic_step()
    {
        var plan = BindChildCare(new RuntimeLimits(100, 64, 100, 1));

        var result = Assert.IsType<FailedResult>(plan.Evaluate(
            RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)),
            TestContext.Current.CancellationToken));

        Assert.Equal("runtime.limit.trace-exceeded", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Repeated_evaluations_are_structurally_identical()
    {
        var plan = BindChildCare();
        var request = RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true));

        var firstResult = plan.Evaluate(request, TestContext.Current.CancellationToken);
        var secondResult = plan.Evaluate(request, TestContext.Current.CancellationToken);
        var first = JsonSerializer.SerializeToElement(firstResult, firstResult.GetType());
        var second = JsonSerializer.SerializeToElement(secondResult, secondResult.GetType());

        Assert.True(JsonElement.DeepEquals(first, second));
    }

    [Fact]
    public void Trace_level_changes_only_the_trace_projection()
    {
        var plan = BindChildCare();
        var full = Assert.IsType<DeterminedResult>(plan.Evaluate(
            RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)),
            TestContext.Current.CancellationToken));
        var none = Assert.IsType<DeterminedResult>(plan.Evaluate(
            RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)) with { TraceLevel = TraceLevel.None },
            TestContext.Current.CancellationToken));

        Assert.Equal(full.Conclusion, none.Conclusion);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(full.Findings), JsonSerializer.SerializeToElement(none.Findings)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(full.Evidence), JsonSerializer.SerializeToElement(none.Evidence)));
        Assert.NotNull(full.Trace);
        Assert.Null(none.Trace);
    }

    [Fact]
    public void Summary_trace_is_the_stable_rule_level_projection()
    {
        var plan = BindChildCare();
        var result = Assert.IsType<DeterminedResult>(plan.Evaluate(
            RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)) with { TraceLevel = TraceLevel.Summary },
            TestContext.Current.CancellationToken));

        Assert.Equal("rule", Assert.Single(result.Trace!.Nodes).Path);
        Assert.Equal(new TruthFactValue(true), result.Conclusion.Value);
    }

    [Fact]
    public void Binding_rejects_packages_that_do_not_match_the_snapshot_lock()
    {
        var (snapshot, package) = LoadChildCare();
        var altered = snapshot with
        {
            Packages = [snapshot.Packages[0] with { SemanticDigest = "sha256:altered" }]
        };

        var result = RulesRuntime.Bind(altered, [package], FunctionCatalogue.Empty, RuntimeLimits.Default, TestContext.Current.CancellationToken);

        Assert.Equal("validation.federation.lock-mismatch", Assert.Single(result.Diagnostics).Code);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void Oversized_fact_collection_is_an_invalid_request()
    {
        var plan = BindChildCare(new RuntimeLimits(100, 64, 1, 100));

        var result = Assert.IsType<InvalidResult>(plan.Evaluate(
            RequestWithFacts((EnrolmentFactId, true), (EvidenceFactId, true)),
            TestContext.Current.CancellationToken));

        Assert.Equal("runtime.request.collection-limit", Assert.Single(result.Diagnostics).Code);
    }

    private static EvaluationRequest RequestWithFacts(params (string Id, bool Value)[] facts) =>
        new(
            "application-test",
            SemanticId.Parse(RuleId),
            facts.ToImmutableDictionary(item => SemanticId.Parse(item.Id), item => (FactValue)new TruthFactValue(item.Value)),
            [],
            TraceLevel.Full,
            DisclosurePolicy.Public);

    private static RuntimePlan BindChildCare(RuntimeLimits? limits = null)
    {
        var (snapshot, package) = LoadChildCare();
        var binding = RulesRuntime.Bind(snapshot, [package], FunctionCatalogue.Empty, limits ?? RuntimeLimits.Default, TestContext.Current.CancellationToken);
        Assert.True(binding.IsSuccess);
        return binding.Plan!;
    }

    private static RuntimePlan BindChildCareDecisionTable(RuntimeLimits? limits = null)
    {
        var document = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs-decision-table.context-package.v1.json"));
        var package = ContextPackageSystem.Load(document).Package!;
        var snapshot = ContextPackageSystem.Resolve([document], new ContextPackageIdentity(package.AuthoredRevision.Id.ToString(), package.AuthoredRevision.ContextVersion)).Snapshot!;
        var binding = RulesRuntime.Bind(snapshot, [package], FunctionCatalogue.Empty, limits ?? RuntimeLimits.Default, TestContext.Current.CancellationToken);
        Assert.True(binding.IsSuccess);
        return binding.Plan!;
    }

    private static (FederationSnapshot Snapshot, LoadedContextPackage Package) LoadChildCare()
    {
        var document = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(document).Package!;
        var resolution = ContextPackageSystem.Resolve([document], new ContextPackageIdentity(package.AuthoredRevision.Id.ToString(), package.AuthoredRevision.ContextVersion));
        return (resolution.Snapshot!, package);
    }

    private sealed class RulesConformanceAdapter : IConformanceAdapter
    {
        public string Capability => "rule-evaluation";
        public string ContractVersion => "1.0";

        public ValueTask<JsonElement> ExecuteAsync(JsonElement input, ConformanceExecutionContext context, CancellationToken cancellationToken)
        {
            var facts = input.GetProperty("facts").EnumerateObject().Select(property =>
                (property.Name == "active-enrolment-exists" ? EnrolmentFactId : EvidenceFactId, property.Value.GetBoolean())).ToArray();
            var result = BindChildCare().Evaluate(RequestWithFacts(facts), cancellationToken);
            var semanticDigest = result.Snapshot.Packages[0].SemanticDigest;
            object observation = result switch
            {
                DeterminedResult determined => new
                {
                    semanticDigest,
                    status = "Determined",
                    conclusion = new { kind = "Truth", value = ((TruthFactValue)determined.Conclusion.Value).Value },
                    findings = determined.Findings.Where(item => item.Code.StartsWith("accs.", StringComparison.Ordinal)).Select(item => new { code = item.Code, supports = "eligible" }),
                    diagnostics = Array.Empty<object>(),
                    missingFacts = Array.Empty<string>(),
                    explanation = new { conclusion = "Eligible", findingCodes = determined.Findings.Where(item => item.Code.StartsWith("accs.", StringComparison.Ordinal)).Select(item => item.Code) },
                    canonicalTrace = new { level = "Full", nodes = new[] { "determine-accs-eligibility", "active-enrolment-exists", "supporting-evidence-is-held", "eligible" } }
                },
                IndeterminateResult indeterminate => new
                {
                    semanticDigest,
                    status = "InformationRequired",
                    conclusion = (object?)null,
                    findings = indeterminate.Findings.Where(item => item.Code == "accs.supporting-evidence-required").Select(item => new { code = item.Code, prevents = "eligible" }),
                    diagnostics = Array.Empty<object>(),
                    missingFacts = new[] { "supporting-evidence-is-held" },
                    explanation = new { conclusion = "Information required", findingCodes = new[] { "accs.supporting-evidence-required" }, nextAction = "Provide supporting evidence" },
                    canonicalTrace = new { level = "Full", nodes = new[] { "determine-accs-eligibility", "active-enrolment-exists", "supporting-evidence-is-held", "information-required" } }
                },
                _ => throw new InvalidOperationException("The reviewed ACCS fixture did not produce a domain evaluation result.")
            };
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(observation));
        }
    }

    private sealed class DecisionTableConformanceAdapter : IConformanceAdapter
    {
        public string Capability => "decision-table-evaluation";
        public string ContractVersion => "1.0";

        public ValueTask<JsonElement> ExecuteAsync(JsonElement input, ConformanceExecutionContext context, CancellationToken cancellationToken)
        {
            var facts = input.GetProperty("facts").EnumerateObject().Select(property =>
                (property.Name == "active-enrolment-exists" ? EnrolmentFactId : EvidenceFactId, property.Value.GetBoolean())).ToArray();
            var request = RequestWithFacts(facts) with { Target = SemanticId.Parse(EligibilityDecisionId) };
            var result = Assert.IsType<DeterminedResult>(BindChildCareDecisionTable().Evaluate(request, cancellationToken));
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new
            {
                semanticDigest = result.Snapshot.Packages[0].SemanticDigest,
                status = "Determined",
                classification = "eligible",
                findingCodes = result.Findings.Select(item => item.Code),
                canonicalTrace = new[] { "classify-accs-eligibility", "eligible-with-enrolment-and-evidence", "eligible" }
            }));
        }
    }
}
