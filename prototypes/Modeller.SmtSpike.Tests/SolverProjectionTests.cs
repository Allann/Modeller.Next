using System.Collections.Immutable;
using System.Text.Json;
using Modeller.Contexts;
using Modeller.Model;
using Modeller.Rules;
using Xunit;

namespace Modeller.SmtSpike.Tests;

public sealed class SolverProjectionTests
{
    [Fact]
    public void Every_child_care_assignment_agrees_with_the_reference_interpreter()
    {
        var (rule, plan) = LoadRuleAndPlan();
        var factIds = rule.InputFacts.Select(item => item.TargetId).OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();

        for (var combination = 0; combination < 1 << factIds.Length; combination++)
        {
            var facts = factIds.Select((id, index) => (Id: id, Value: (combination & 1 << index) != 0))
                .ToImmutableDictionary(item => item.Id, item => (FactValue)new TruthFactValue(item.Value));
            var reference = Assert.IsType<DeterminedResult>(plan.Evaluate(new EvaluationRequest(
                $"assignment-{combination}", rule.Id, facts, [], TraceLevel.None, DisclosurePolicy.Public), TestContext.Current.CancellationToken));
            var solver = SolverProjection.Evaluate(rule, facts.ToDictionary(item => item.Key, item => ((TruthFactValue)item.Value).Value), cancellationToken: TestContext.Current.CancellationToken);

            var expected = ((TruthFactValue)reference.Conclusion.Value).Value
                ? SolverStatus.Sat
                : SolverStatus.Unsat;
            Assert.Equal(expected, solver.Status);
        }
    }

    [Fact]
    public void Contradictory_named_facts_return_a_conflict_core()
    {
        var (rule, _) = LoadRuleAndPlan();
        var fact = rule.InputFacts[0].TargetId;

        var result = SolverProjection.CheckClaim(rule,
            [(fact, true, $"fact:{fact}:reported-true"), (fact, false, $"fact:{fact}:reported-false")], true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("inconsistent-or-invalid", result.Classification);
        Assert.Equal(SolverStatus.Unsat, result.Consistency.Status);
        Assert.Equal([$"fact:{fact}:reported-false", $"fact:{fact}:reported-true"], result.Consistency.ConflictCore);
    }

    [Fact]
    public void Decision_analysis_finds_overlap_gap_and_unreachable_conclusion()
    {
        var (rule, _) = LoadRuleAndPlan();
        var first = rule.InputFacts[0];
        var second = rule.InputFacts[1];
        var reachable = rule.Conclusions[0];
        var unreachable = reachable with { Id = SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000099") };
        var rows = ImmutableArray.Create(
            Row("0191f6d4-4ea0-7000-8000-000000000091", first, true, second, null, reachable.Id),
            Row("0191f6d4-4ea0-7000-8000-000000000092", first, true, second, true, reachable.Id));
        var decision = new DecisionDefinition(
            SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000090"), rule.Name, rule.Slug,
            rule.InputFacts, [reachable, unreachable], new DecisionTable(DecisionHitPolicy.Unique, rows));

        var result = SolverProjection.Analyze(decision, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Overlaps);
        Assert.Equal(SolverStatus.Sat, result.Coverage.Status);
        Assert.Equal(SolverStatus.Unsat, result.Conclusions.Single(item => item.Conclusion == unreachable.Id).Reachability.Status);
    }

    [Fact]
    public void Changed_rule_returns_a_semantic_difference_witness()
    {
        var (rule, _) = LoadRuleAndPlan();
        var changed = rule with { Expression = new FactExpression(rule.InputFacts[0]) };

        var result = SolverProjection.FindSemanticChange(rule, changed, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SolverStatus.Sat, result.Status);
        Assert.True(result.Model[rule.InputFacts[0].TargetId.ToString()]);
        Assert.False(result.Model[rule.InputFacts[1].TargetId.ToString()]);
    }

    [Fact]
    public void Cancellation_is_not_a_domain_answer()
    {
        var (rule, _) = LoadRuleAndPlan();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = SolverProjection.Evaluate(rule, ImmutableDictionary<SemanticId, bool>.Empty, cancellationToken: cancellation.Token);

        Assert.Equal(SolverStatus.Cancelled, result.Status);
    }

    [Fact]
    public void Unsupported_syntax_fails_closed_with_a_stable_diagnostic()
    {
        var (rule, _) = LoadRuleAndPlan();
        var unsupported = rule with { Expression = new UnsupportedExpression() };

        var exception = Assert.Throws<UnsupportedProjectionException>(() => SolverProjection.Evaluate(
            unsupported, ImmutableDictionary<SemanticId, bool>.Empty,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("solver.translation.unsupported-expression", exception.Diagnostic.Code);
        Assert.Equal(rule.Id.ToString(), exception.Diagnostic.SemanticId);
    }

    [Fact]
    public void Identical_queries_are_structurally_reproducible()
    {
        var (rule, _) = LoadRuleAndPlan();
        var facts = rule.InputFacts.ToDictionary(item => item.TargetId, _ => true);

        var first = SolverProjection.Evaluate(rule, facts, cancellationToken: TestContext.Current.CancellationToken);
        var second = SolverProjection.Evaluate(rule, facts, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(JsonElement.DeepEquals(
            JsonSerializer.SerializeToElement(first),
            JsonSerializer.SerializeToElement(second)));
    }

    [Fact]
    public void Resource_exhaustion_is_unknown_and_not_a_domain_answer()
    {
        var (rule, _) = LoadRuleAndPlan();

        var result = SolverProjection.Evaluate(
            rule, ImmutableDictionary<SemanticId, bool>.Empty, new SolverBudget(2_000, 1),
            TestContext.Current.CancellationToken);

        Assert.Equal(SolverStatus.Unknown, result.Status);
    }

    private static DecisionRow Row(string id, FactReference first, bool? firstValue, FactReference second, bool? secondValue, SemanticId conclusion) =>
        new(SemanticId.Parse(id), new SemanticName("Spike row"), new SemanticSlug($"spike-row-{id[^2..]}"),
            [new TruthDecisionCondition(first, firstValue), new TruthDecisionCondition(second, secondValue)],
            new ConclusionReference(conclusion), "spike.finding");

    private static (RuleDefinition Rule, RuntimePlan Plan) LoadRuleAndPlan()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(bytes).Package!;
        var snapshot = ContextPackageSystem.Resolve([bytes], new ContextPackageIdentity(package.AuthoredRevision.Id.ToString(), package.AuthoredRevision.ContextVersion), TestContext.Current.CancellationToken).Snapshot!;
        var plan = RulesRuntime.Bind(snapshot, [package], FunctionCatalogue.Empty, RuntimeLimits.Default, TestContext.Current.CancellationToken).Plan!;
        return (package.AuthoredRevision.Definitions.OfType<RuleDefinition>().Single(), plan);
    }

    private sealed record UnsupportedExpression : RuleExpression;
}
