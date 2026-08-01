using System.Collections.Immutable;
using Modeller.Contexts;
using Modeller.Model;
using Modeller.Validation;

namespace Modeller.Rules;

public static class RulesRuntime
{
    public const string RuntimeVersion = "reference/1.0";

    public static RuntimeBindingResult Bind(
        FederationSnapshot snapshot,
        IEnumerable<LoadedContextPackage> packages,
        FunctionCatalogue functions,
        RuntimeLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(limits);
        cancellationToken.ThrowIfCancellationRequested();
        var supplied = packages.ToImmutableArray();
        var validation = SemanticValidation.Validate(
            ValidationRequest.For(snapshot, supplied, ValidationProfile.Canonical),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = validation.Diagnostics.Select(item => new RuntimeDiagnostic(
            item.Code,
            RuntimeDiagnosticStage.Binding,
            item.Message,
            item.SubjectId)).ToImmutableArray().ToBuilder();
        var rules = supplied.SelectMany(package => package.AuthoredRevision.Definitions.OfType<RuleDefinition>())
            .OrderBy(rule => rule.Id.ToString(), StringComparer.Ordinal)
            .ToImmutableDictionary(rule => rule.Id);
        var decisions = supplied.SelectMany(package => package.AuthoredRevision.Definitions.OfType<DecisionDefinition>())
            .OrderBy(decision => decision.Id.ToString(), StringComparer.Ordinal)
            .ToImmutableDictionary(decision => decision.Id);
        foreach (var rule in rules.Values.Where(rule => rule.Expression is null))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.binding.expression-required",
                RuntimeDiagnosticStage.Binding,
                "A bound rule requires a canonical expression.",
                rule.Id.ToString()));
        }
        foreach (var rule in rules.Values.Where(rule => rule.Expression is not null && ExpressionDepth(rule.Expression) > limits.MaximumExpressionDepth))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.binding.expression-depth-exceeded",
                RuntimeDiagnosticStage.Binding,
                "A rule expression exceeds the configured deterministic depth limit.",
                rule.Id.ToString()));
        }
        foreach (var decision in decisions.Values)
        {
            ValidateDecision(decision, diagnostics);
        }

        var ordered = diagnostics.OrderBy(item => item.SemanticId, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToImmutableArray();
        return ordered.Length > 0
            ? new RuntimeBindingResult(null, ordered)
            : new RuntimeBindingResult(new RuntimePlan(snapshot, rules, decisions, functions, limits), []);
    }

    private static void ValidateDecision(DecisionDefinition decision, ImmutableArray<RuntimeDiagnostic>.Builder diagnostics)
    {
        var inputs = decision.InputFacts.Select(item => item.TargetId).ToHashSet();
        var conclusions = decision.Conclusions.Select(item => item.Id).ToHashSet();
        var invalidRow = false;
        foreach (var row in decision.Table.Rows)
        {
            if (row.Conditions.Length != inputs.Count ||
                row.Conditions.Select(item => item.Fact.TargetId).ToHashSet().SetEquals(inputs) is false ||
                !conclusions.Contains(row.Conclusion.TargetId))
            {
                invalidRow = true;
                diagnostics.Add(new RuntimeDiagnostic(
                    "runtime.binding.decision-row-invalid", RuntimeDiagnosticStage.Binding,
                    "Every decision row must cover the declared inputs and select a declared conclusion.", row.Id.ToString()));
            }
        }
        if (invalidRow) return;

        for (var left = 0; left < decision.Table.Rows.Length; left++)
            for (var right = left + 1; right < decision.Table.Rows.Length; right++)
            {
                if (RowsOverlap(decision.Table.Rows[left], decision.Table.Rows[right]))
                {
                    diagnostics.Add(new RuntimeDiagnostic(
                        "runtime.binding.decision-rows-overlap", RuntimeDiagnosticStage.Binding,
                        "Unique decision-table rows must not overlap.", decision.Id.ToString()));
                }
            }

        if (inputs.Count > 16)
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.binding.decision-table-limit", RuntimeDiagnosticStage.Binding,
                "Truth decision tables may declare at most 16 inputs.", decision.Id.ToString()));
        }
        else if (!IsExhaustive(decision, inputs.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray()))
        {
            diagnostics.Add(new RuntimeDiagnostic(
                "runtime.binding.decision-table-incomplete", RuntimeDiagnosticStage.Binding,
                "A Unique decision table must match every complete typed input combination.", decision.Id.ToString()));
        }
    }

    private static bool IsExhaustive(DecisionDefinition decision, SemanticId[] inputs)
    {
        for (var combination = 0; combination < 1 << inputs.Length; combination++)
        {
            var matching = decision.Table.Rows.Count(row => row.Conditions.All(condition =>
            {
                if (condition.Expected is null) return true;
                var index = Array.IndexOf(inputs, condition.Fact.TargetId);
                return condition.Expected == ((combination & 1 << index) != 0);
            }));
            if (matching != 1) return false;
        }
        return true;
    }

    private static bool RowsOverlap(DecisionRow left, DecisionRow right)
    {
        var rightConditions = right.Conditions.ToDictionary(item => item.Fact.TargetId);
        return left.Conditions.All(condition =>
        {
            var other = rightConditions[condition.Fact.TargetId];
            return condition.Expected is null || other.Expected is null || condition.Expected == other.Expected;
        });
    }

    private static int ExpressionDepth(RuleExpression expression) => expression switch
    {
        FactExpression => 1,
        AndExpression group when group.Operands.IsEmpty => 1,
        AndExpression group => 1 + group.Operands.Max(ExpressionDepth),
        _ => int.MaxValue
    };
}

public sealed class RuntimePlan
{
    private readonly ImmutableDictionary<SemanticId, RuleDefinition> rules;
    private readonly ImmutableDictionary<SemanticId, DecisionDefinition> decisions;
    private readonly FunctionCatalogue functions;
    private readonly RuntimeLimits limits;

    internal RuntimePlan(
        FederationSnapshot snapshot,
        ImmutableDictionary<SemanticId, RuleDefinition> rules,
        ImmutableDictionary<SemanticId, DecisionDefinition> decisions,
        FunctionCatalogue functions,
        RuntimeLimits limits)
    {
        Snapshot = snapshot;
        this.rules = rules;
        this.decisions = decisions;
        this.functions = functions;
        this.limits = limits;
    }

    public FederationSnapshot Snapshot { get; }
    public string RuntimeVersion => RulesRuntime.RuntimeVersion;
    public string FunctionCatalogueVersion => functions.Version;

    public EvaluationResult Evaluate(EvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        rules.TryGetValue(request.Target, out var rule);
        decisions.TryGetValue(request.Target, out var decision);
        if (rule is null && decision is null)
        {
            return new InvalidResult(Snapshot, request.RequestId, request.Target,
                [new RuntimeDiagnostic("runtime.request.target-unresolved", RuntimeDiagnosticStage.Request, "The requested rule is not present in the bound snapshot.", request.Target.ToString())]);
        }
        if (request.Facts.Count > limits.MaximumCollectionSize || request.Evidence.Length > limits.MaximumCollectionSize)
        {
            return new InvalidResult(Snapshot, request.RequestId, request.Target,
                [new RuntimeDiagnostic("runtime.request.collection-limit", RuntimeDiagnosticStage.Request, "The request exceeds the configured deterministic collection limit.", request.Target.ToString())]);
        }

        var knownFacts = (rule is not null ? rule.InputFacts : decision!.InputFacts)
            .Select(reference => reference.TargetId).ToHashSet();
        var invalidFact = request.Facts.Keys.OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .FirstOrDefault(id => !knownFacts.Contains(id));
        if (invalidFact != default)
        {
            return new InvalidResult(Snapshot, request.RequestId, request.Target,
                [new RuntimeDiagnostic("runtime.request.fact-undeclared", RuntimeDiagnosticStage.Request, "The request supplied a Fact not declared by the target rule.", invalidFact.ToString())]);
        }

        if (request.Facts.FirstOrDefault(pair => pair.Value is not TruthFactValue) is var invalid && invalid.Key != default)
        {
            return new InvalidResult(Snapshot, request.RequestId, request.Target,
                [new RuntimeDiagnostic("runtime.request.fact-type-mismatch", RuntimeDiagnosticStage.Request, "A supplied Fact value does not match its declared type.", invalid.Key.ToString())]);
        }

        if (decision is not null)
        {
            return EvaluateDecision(request, decision, cancellationToken);
        }

        var boundRule = rule!;
        var state = new EvaluationState(request, limits, cancellationToken);
        var evaluated = state.Evaluate(boundRule.Expression!, "rule/and");
        cancellationToken.ThrowIfCancellationRequested();
        var trace = state.Trace(boundRule.Id, boundRule.Conclusions[0].Id, evaluated);
        if (state.LimitExceeded)
        {
            return new FailedResult(Snapshot, request.RequestId, request.Target,
                [new RuntimeDiagnostic(state.LimitCode!, RuntimeDiagnosticStage.Evaluation, "Evaluation exceeded a deterministic runtime limit.", boundRule.Id.ToString())],
                trace);
        }

        var rawFindings = state.Findings.Append(new Finding(
                $"finding-{state.Findings.Count + 1:D4}",
                evaluated is null ? "evaluation.rule.indeterminate" : "evaluation.rule.determined",
                evaluated is null ? FindingDisposition.Preventing : FindingDisposition.Supporting,
                boundRule.Id,
                [],
                []))
            .ToImmutableArray();
        var evidence = request.Evidence
            .Where(item => item.FactIds.Any(state.ReferencedFacts.Contains))
            .Where(item => request.Disclosure == DisclosurePolicy.Audit || item.Sensitivity == EvidenceSensitivity.Public)
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        var permittedEvidenceIds = evidence.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var findings = rawFindings.Select(item => item with
        {
            EvidenceIds = item.EvidenceIds.Where(permittedEvidenceIds.Contains).ToImmutableArray()
        })
            .ToImmutableArray();
        return evaluated is bool value
            ? new DeterminedResult(
                Snapshot, request.RequestId, request.Target,
                new EvaluationConclusion(boundRule.Conclusions[0].Id, new TruthFactValue(value)), findings, evidence, trace)
            : new IndeterminateResult(
                Snapshot, request.RequestId, request.Target,
                state.MissingFacts.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToImmutableArray(), findings, evidence, trace);
    }

    private EvaluationResult EvaluateDecision(
        EvaluationRequest request,
        DecisionDefinition decision,
        CancellationToken cancellationToken)
    {
        var missing = new Dictionary<SemanticId, string?>();
        var referenced = new HashSet<SemanticId>();
        var traceNodes = new List<TraceNode>();
        var steps = 0;
        for (var rowIndex = 0; rowIndex < decision.Table.Rows.Length; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = decision.Table.Rows[rowIndex];
            var rowMissing = new List<TruthDecisionCondition>();
            var mismatched = false;
            foreach (var condition in row.Conditions)
            {
                if (++steps > limits.MaximumSteps)
                {
                    return new FailedResult(Snapshot, request.RequestId, request.Target,
                        [new RuntimeDiagnostic("runtime.limit.work-exceeded", RuntimeDiagnosticStage.Evaluation, "Evaluation exceeded a deterministic runtime limit.", decision.Id.ToString())],
                        DecisionTrace(request, decision.Id, traceNodes, TraceNodeStatus.Failed));
                }
                if (condition.Expected is null) continue;
                referenced.Add(condition.Fact.TargetId);
                if (!request.Facts.TryGetValue(condition.Fact.TargetId, out var supplied))
                {
                    rowMissing.Add(condition);
                    continue;
                }
                if (((TruthFactValue)supplied).Value != condition.Expected)
                {
                    mismatched = true;
                    break;
                }
            }

            if (mismatched) continue;
            if (rowMissing.Count > 0)
            {
                foreach (var condition in rowMissing)
                    missing.TryAdd(condition.Fact.TargetId, condition.MissingFindingCode);
                continue;
            }

            AddDecisionTraceNode(request, traceNodes, $"decision/row/{rowIndex}", row.Id, TraceNodeStatus.Determined);
            if (traceNodes.Count > limits.MaximumTraceNodes)
            {
                return new FailedResult(Snapshot, request.RequestId, request.Target,
                    [new RuntimeDiagnostic("runtime.limit.trace-exceeded", RuntimeDiagnosticStage.Evaluation, "Evaluation exceeded a deterministic runtime limit.", decision.Id.ToString())],
                    DecisionTrace(request, decision.Id, traceNodes.Take(limits.MaximumTraceNodes).ToList(), TraceNodeStatus.Failed));
            }
            var evidence = PermittedEvidence(request, referenced);
            var finding = new Finding("finding-0001", row.FindingCode, FindingDisposition.Supporting, decision.Id,
                referenced.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToImmutableArray(),
                evidence.Select(item => item.Id).ToImmutableArray());
            AddDecisionTraceNode(request, traceNodes, "decision/conclusion", row.Conclusion.TargetId, TraceNodeStatus.Determined);
            if (traceNodes.Count > limits.MaximumTraceNodes)
            {
                return new FailedResult(Snapshot, request.RequestId, request.Target,
                    [new RuntimeDiagnostic("runtime.limit.trace-exceeded", RuntimeDiagnosticStage.Evaluation, "Evaluation exceeded a deterministic runtime limit.", decision.Id.ToString())],
                    DecisionTrace(request, decision.Id, traceNodes.Take(limits.MaximumTraceNodes).ToList(), TraceNodeStatus.Failed));
            }
            return new DeterminedResult(
                Snapshot, request.RequestId, request.Target,
                new EvaluationConclusion(row.Conclusion.TargetId, new ClassificationFactValue(row.Conclusion.TargetId)),
                [finding], evidence,
                DecisionTrace(request, decision.Id, traceNodes, TraceNodeStatus.Determined));
        }

        if (missing.Count > 0)
        {
            var evidence = PermittedEvidence(request, referenced);
            var findings = missing.OrderBy(item => item.Key.ToString(), StringComparer.Ordinal)
                .Select((item, index) => new Finding(
                    $"finding-{index + 1:D4}", item.Value ?? "evaluation.fact.missing",
                    FindingDisposition.Preventing, decision.Id, [item.Key], []))
                .ToImmutableArray();
            AddDecisionTraceNode(request, traceNodes, "decision/information-required", decision.Id, TraceNodeStatus.Missing);
            return new IndeterminateResult(
                Snapshot, request.RequestId, request.Target,
                missing.Keys.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToImmutableArray(),
                findings, evidence,
                DecisionTrace(request, decision.Id, traceNodes, TraceNodeStatus.Missing));
        }

        return new FailedResult(Snapshot, request.RequestId, request.Target,
            [new RuntimeDiagnostic("runtime.decision.no-match", RuntimeDiagnosticStage.Evaluation, "No decision-table row matched the supplied facts.", decision.Id.ToString())],
            DecisionTrace(request, decision.Id, traceNodes, TraceNodeStatus.Failed));
    }

    private static ImmutableArray<EvidenceRecord> PermittedEvidence(EvaluationRequest request, HashSet<SemanticId> referenced) =>
        request.Evidence.Where(item => item.FactIds.Any(referenced.Contains))
            .Where(item => request.Disclosure == DisclosurePolicy.Audit || item.Sensitivity == EvidenceSensitivity.Public)
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToImmutableArray();

    private static void AddDecisionTraceNode(
        EvaluationRequest request,
        List<TraceNode> nodes,
        string path,
        SemanticId semanticId,
        TraceNodeStatus status)
    {
        if (request.TraceLevel == TraceLevel.Full)
            nodes.Add(new TraceNode(path, semanticId, status, null));
    }

    private static CanonicalTrace? DecisionTrace(
        EvaluationRequest request,
        SemanticId decisionId,
        List<TraceNode> nodes,
        TraceNodeStatus status) =>
        request.TraceLevel == TraceLevel.None
            ? null
            : new CanonicalTrace([new TraceNode("decision", decisionId, status, null), .. nodes]);

    private sealed class EvaluationState(EvaluationRequest request, RuntimeLimits limits, CancellationToken cancellationToken)
    {
        private readonly List<Finding> findings = [];
        private readonly List<TraceNode> nodes = [];
        private int steps;

        public IReadOnlyList<Finding> Findings => findings;
        public HashSet<SemanticId> MissingFacts { get; } = [];
        public HashSet<SemanticId> ReferencedFacts { get; } = [];
        public bool LimitExceeded { get; private set; }
        public string? LimitCode { get; private set; }

        public bool? Evaluate(RuleExpression expression, string path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++steps > limits.MaximumSteps)
            {
                LimitExceeded = true;
                LimitCode = "runtime.limit.work-exceeded";
                return null;
            }

            return expression switch
            {
                FactExpression fact => EvaluateFact(fact, path),
                AndExpression and => EvaluateAnd(and, path),
                _ => throw new InvalidOperationException("The bound plan contains an unsupported expression.")
            };
        }

        private bool? EvaluateFact(FactExpression expression, string path)
        {
            var id = expression.Fact.TargetId;
            ReferencedFacts.Add(id);
            if (!request.Facts.TryGetValue(id, out var supplied))
            {
                MissingFacts.Add(id);
                findings.Add(FindingFor(expression.MissingFindingCode ?? "evaluation.fact.missing", FindingDisposition.Preventing, id));
                AddNode(path, id, TraceNodeStatus.Missing, null);
                return null;
            }

            var value = ((TruthFactValue)supplied).Value;
            var code = value
                ? expression.TrueFindingCode ?? "evaluation.fact.true"
                : expression.FalseFindingCode ?? "evaluation.fact.false";
            findings.Add(FindingFor(code, value ? FindingDisposition.Supporting : FindingDisposition.Preventing, id));
            AddNode(path, id, TraceNodeStatus.Determined, new TruthFactValue(value));
            return value;
        }

        private bool? EvaluateAnd(AndExpression expression, string path)
        {
            var missing = false;
            for (var index = 0; index < expression.Operands.Length; index++)
            {
                var value = Evaluate(expression.Operands[index], $"{path}/{index}");
                if (LimitExceeded) return null;
                if (value is false) return false;
                if (value is null) missing = true;
            }

            return missing ? null : true;
        }

        private Finding FindingFor(string code, FindingDisposition disposition, SemanticId fact) =>
            new($"finding-{findings.Count + 1:D4}", code, disposition, request.Target, [fact],
                request.Evidence.Where(item => item.FactIds.Contains(fact)).Select(item => item.Id).Order(StringComparer.Ordinal).ToImmutableArray());

        private void AddNode(string path, SemanticId semanticId, TraceNodeStatus status, FactValue? value)
        {
            if (request.TraceLevel != TraceLevel.Full) return;
            if (nodes.Count >= limits.MaximumTraceNodes)
            {
                LimitExceeded = true;
                LimitCode = "runtime.limit.trace-exceeded";
                return;
            }
            nodes.Add(new TraceNode(path, semanticId, status, request.Disclosure == DisclosurePolicy.Audit ? value : null));
        }

        public CanonicalTrace? Trace(SemanticId ruleId, SemanticId? conclusionId = null, bool? result = null)
        {
            if (request.TraceLevel == TraceLevel.None) return null;
            if (conclusionId is not null && request.TraceLevel == TraceLevel.Full)
            {
                AddNode(
                    result is null ? "rule/information-required" : "rule/conclusion",
                    result is null ? ruleId : conclusionId.Value,
                    result is null ? TraceNodeStatus.Missing : TraceNodeStatus.Determined,
                    result is bool value ? new TruthFactValue(value) : null);
            }
            var status = LimitExceeded ? TraceNodeStatus.Failed : MissingFacts.Count > 0 ? TraceNodeStatus.Missing : TraceNodeStatus.Determined;
            return new CanonicalTrace([new TraceNode("rule", ruleId, status, null), .. nodes]);
        }
    }
}

public sealed record RuntimeBindingResult(RuntimePlan? Plan, ImmutableArray<RuntimeDiagnostic> Diagnostics)
{
    public bool IsSuccess => Plan is not null && Diagnostics.IsEmpty;
}

public sealed record RuntimeLimits
{
    public RuntimeLimits(int maximumSteps, int maximumExpressionDepth, int maximumCollectionSize, int maximumTraceNodes)
    {
        MaximumSteps = Positive(maximumSteps, nameof(maximumSteps));
        MaximumExpressionDepth = Positive(maximumExpressionDepth, nameof(maximumExpressionDepth));
        MaximumCollectionSize = Positive(maximumCollectionSize, nameof(maximumCollectionSize));
        MaximumTraceNodes = Positive(maximumTraceNodes, nameof(maximumTraceNodes));
    }
    public int MaximumSteps { get; }
    public int MaximumExpressionDepth { get; }
    public int MaximumCollectionSize { get; }
    public int MaximumTraceNodes { get; }
    public static RuntimeLimits Default { get; } = new(100_000, 64, 10_000, 10_000);
    private static int Positive(int value, string name) => value > 0 ? value : throw new ArgumentOutOfRangeException(name);
}

public sealed record FunctionCatalogue
{
    public FunctionCatalogue(string version, ImmutableArray<IDeclaredFunctionAdapter> adapters)
    {
        Version = string.IsNullOrWhiteSpace(version) ? throw new ArgumentException("A function catalogue version is required.", nameof(version)) : version;
        Adapters = adapters.IsDefault ? [] : adapters;
        if (Adapters.GroupBy(item => item.Id, StringComparer.Ordinal).Any(group => group.Skip(1).Any()))
            throw new ArgumentException("Declared function IDs must be unique within a catalogue.", nameof(adapters));
    }
    public string Version { get; }
    public ImmutableArray<IDeclaredFunctionAdapter> Adapters { get; }
    public static FunctionCatalogue Empty { get; } = new("none/1.0", []);
}

public interface IDeclaredFunctionAdapter
{
    string Id { get; }
    string Version { get; }
    ValueTask<DeclaredFunctionResult> InvokeAsync(ImmutableArray<FactValue> arguments, CancellationToken cancellationToken);
}

public abstract record DeclaredFunctionResult;
public sealed record DeclaredFunctionValue(FactValue Value) : DeclaredFunctionResult;
public sealed record DeclaredFunctionFailure(string Code, string SafeCauseCategory) : DeclaredFunctionResult;

public abstract record FactValue;
public sealed record TruthFactValue(bool Value) : FactValue;
public sealed record TextFactValue(string Value) : FactValue;
public sealed record ClassificationFactValue(SemanticId Value) : FactValue;
public sealed record EvaluationRequest(string RequestId, SemanticId Target, ImmutableDictionary<SemanticId, FactValue> Facts, ImmutableArray<EvidenceRecord> Evidence, TraceLevel TraceLevel, DisclosurePolicy Disclosure);
public sealed record EvidenceRecord(string Id, string Kind, string SourceReference, string? ContentDigest, ImmutableArray<SemanticId> FactIds, EvidenceSensitivity Sensitivity);
public enum EvidenceSensitivity { Public, Protected }
public enum DisclosurePolicy { Public, Audit }
public enum TraceLevel { None, Summary, Full }
public enum FindingDisposition { Supporting, Qualifying, Preventing }
public sealed record Finding(string LocalId, string Code, FindingDisposition Disposition, SemanticId Rule, ImmutableArray<SemanticId> Facts, ImmutableArray<string> EvidenceIds);
public sealed record EvaluationConclusion(SemanticId Id, FactValue Value);
public enum TraceNodeStatus { Determined, Missing, Failed, Skipped }
public sealed record TraceNode(string Path, SemanticId SemanticId, TraceNodeStatus Status, FactValue? Result);
public sealed record CanonicalTrace(ImmutableArray<TraceNode> Nodes);
public enum RuntimeDiagnosticStage { Binding, Request, Evaluation, Function }
public sealed record RuntimeDiagnostic(string Code, RuntimeDiagnosticStage Stage, string Message, string? SemanticId = null);

public abstract record EvaluationResult(FederationSnapshot Snapshot, string RequestId, SemanticId Target);
public sealed record DeterminedResult(FederationSnapshot Snapshot, string RequestId, SemanticId Target, EvaluationConclusion Conclusion, ImmutableArray<Finding> Findings, ImmutableArray<EvidenceRecord> Evidence, CanonicalTrace? Trace) : EvaluationResult(Snapshot, RequestId, Target);
public sealed record IndeterminateResult(FederationSnapshot Snapshot, string RequestId, SemanticId Target, ImmutableArray<SemanticId> MissingFacts, ImmutableArray<Finding> Findings, ImmutableArray<EvidenceRecord> Evidence, CanonicalTrace? Trace) : EvaluationResult(Snapshot, RequestId, Target);
public sealed record InvalidResult(FederationSnapshot Snapshot, string RequestId, SemanticId Target, ImmutableArray<RuntimeDiagnostic> Diagnostics) : EvaluationResult(Snapshot, RequestId, Target);
public sealed record FailedResult(FederationSnapshot Snapshot, string RequestId, SemanticId Target, ImmutableArray<RuntimeDiagnostic> Diagnostics, CanonicalTrace? Trace) : EvaluationResult(Snapshot, RequestId, Target);
