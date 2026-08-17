using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Z3;
using Modeller.Model;

namespace Modeller.SmtSpike;

public enum SolverStatus { Sat, Unsat, Unknown, Failed, Cancelled }

public sealed record SolverBudget(uint TimeoutMilliseconds, uint ResourceLimit)
{
    public static SolverBudget Default { get; } = new(2_000, 100_000);
}

public sealed record SolverDiagnostic(string Code, string Message, string? SemanticId = null);

public sealed record SolverAnswer(
    SolverStatus Status,
    ImmutableSortedDictionary<string, bool> Model,
    ImmutableArray<string> ConflictCore,
    string? Detail,
    string QueryDigest);

public sealed record ClaimCheck(
    string Classification,
    SolverAnswer Consistency,
    SolverAnswer Claim,
    SolverAnswer Refutation);

public sealed record DecisionAnalysis(
    ImmutableArray<(SemanticId Left, SemanticId Right, SolverAnswer Witness)> Overlaps,
    SolverAnswer Coverage,
    ImmutableArray<(SemanticId Conclusion, SolverAnswer Reachability)> Conclusions);

public sealed class UnsupportedProjectionException(SolverDiagnostic diagnostic) : Exception(diagnostic.Message)
{
    public SolverDiagnostic Diagnostic { get; } = diagnostic;
}

public static class SolverProjection
{
    public const string TranslatorId = "modeller-truth-and-z3/0.1";
    public const string SolverId = "Microsoft.Z3/4.12.2";

    public static ClaimCheck CheckClaim(
        RuleDefinition rule,
        IReadOnlyList<(SemanticId Fact, bool Value, string AssertionId)> facts,
        bool claim,
        SolverBudget? budget = null,
        CancellationToken cancellationToken = default)
    {
        using var context = NewContext();
        var expression = Translate(context, rule.Expression ?? throw Unsupported(rule.Id, "solver.translation.expression-required"), rule.Id);
        var factAssertions = facts.Select(item => new NamedExpression(item.AssertionId, item.Value
            ? Fact(context, item.Fact)
            : context.MkNot(Fact(context, item.Fact)))).ToImmutableArray();
        var consistency = Solve(context, factAssertions, budget ?? SolverBudget.Default, cancellationToken);
        if (consistency.Status != SolverStatus.Sat)
            return new ClaimCheck("inconsistent-or-invalid", consistency, Skipped(consistency, "claim"), Skipped(consistency, "refutation"));

        var claimExpression = claim ? expression : context.MkNot(expression);
        var claimAnswer = Solve(context, [.. factAssertions, new NamedExpression($"claim:{rule.Conclusions[0].Id}", claimExpression)], budget ?? SolverBudget.Default, cancellationToken);
        var refutation = Solve(context, [.. factAssertions, new NamedExpression($"refutation:{rule.Conclusions[0].Id}", context.MkNot(claimExpression))], budget ?? SolverBudget.Default, cancellationToken);
        var classification = (claimAnswer.Status, refutation.Status) switch
        {
            (SolverStatus.Sat, SolverStatus.Unsat) => "entailed",
            (SolverStatus.Unsat, SolverStatus.Sat) => "contradicted",
            (SolverStatus.Sat, SolverStatus.Sat) => "ambiguous",
            (SolverStatus.Cancelled, _) or (_, SolverStatus.Cancelled) => "cancelled",
            (SolverStatus.Unknown, _) or (_, SolverStatus.Unknown) => "unknown",
            _ => "failed"
        };
        return new ClaimCheck(classification, consistency, claimAnswer, refutation);
    }

    public static SolverAnswer Evaluate(
        RuleDefinition rule,
        IReadOnlyDictionary<SemanticId, bool> facts,
        SolverBudget? budget = null,
        CancellationToken cancellationToken = default)
    {
        using var context = NewContext();
        var expression = Translate(context, rule.Expression ?? throw Unsupported(rule.Id, "solver.translation.expression-required"), rule.Id);
        var assertions = facts.OrderBy(item => item.Key.ToString(), StringComparer.Ordinal)
            .Select(item => new NamedExpression($"fact:{item.Key}", item.Value ? Fact(context, item.Key) : context.MkNot(Fact(context, item.Key))))
            .Append(new NamedExpression($"rule:{rule.Id}", expression));
        return Solve(context, assertions, budget ?? SolverBudget.Default, cancellationToken);
    }

    public static DecisionAnalysis Analyze(
        DecisionDefinition decision,
        SolverBudget? budget = null,
        CancellationToken cancellationToken = default)
    {
        using var context = NewContext();
        var rows = decision.Table.Rows.Select(row => (Row: row, Expression: Translate(context, row))).ToArray();
        var overlaps = ImmutableArray.CreateBuilder<(SemanticId, SemanticId, SolverAnswer)>();
        for (var left = 0; left < rows.Length; left++)
            for (var right = left + 1; right < rows.Length; right++)
            {
                var answer = Solve(context,
                    [new($"row:{rows[left].Row.Id}", rows[left].Expression), new($"row:{rows[right].Row.Id}", rows[right].Expression)],
                    budget ?? SolverBudget.Default, cancellationToken);
                if (answer.Status == SolverStatus.Sat) overlaps.Add((rows[left].Row.Id, rows[right].Row.Id, answer));
            }

        var coverage = Solve(context,
            [new($"decision:{decision.Id}:gap", context.MkNot(context.MkOr(rows.Select(item => item.Expression).ToArray())))],
            budget ?? SolverBudget.Default, cancellationToken);
        var conclusions = decision.Conclusions.Select(conclusion =>
        {
            var matching = rows.Where(item => item.Row.Conclusion.TargetId == conclusion.Id).Select(item => item.Expression).ToArray();
            var expression = matching.Length == 0 ? context.MkFalse() : context.MkOr(matching);
            return (conclusion.Id, Solve(context, [new($"conclusion:{conclusion.Id}", expression)], budget ?? SolverBudget.Default, cancellationToken));
        }).ToImmutableArray();
        return new DecisionAnalysis(overlaps.ToImmutable(), coverage, conclusions);
    }

    public static SolverAnswer FindSemanticChange(
        RuleDefinition before,
        RuleDefinition after,
        SolverBudget? budget = null,
        CancellationToken cancellationToken = default)
    {
        using var context = NewContext();
        var difference = context.MkXor(
            Translate(context, before.Expression ?? throw Unsupported(before.Id, "solver.translation.expression-required"), before.Id),
            Translate(context, after.Expression ?? throw Unsupported(after.Id, "solver.translation.expression-required"), after.Id));
        return Solve(context, [new($"difference:{before.Id}:{after.Id}", difference)], budget ?? SolverBudget.Default, cancellationToken);
    }

    private static Context NewContext() => new(new Dictionary<string, string>
    {
        ["model"] = "true",
        ["unsat_core"] = "true"
    });

    private static BoolExpr Translate(Context context, RuleExpression expression, SemanticId? owner = null) => expression switch
    {
        FactExpression fact => Fact(context, fact.Fact.TargetId),
        AndExpression and => context.MkAnd(and.Operands.Select(item => Translate(context, item, owner)).ToArray()),
        _ => throw Unsupported(owner, "solver.translation.unsupported-expression")
    };

    private static BoolExpr Translate(Context context, DecisionRow row) => context.MkAnd(row.Conditions
        .Where(item => item.Expected is not null)
        .Select(item => item.Expected!.Value ? Fact(context, item.Fact.TargetId) : context.MkNot(Fact(context, item.Fact.TargetId)))
        .ToArray());

    private static BoolExpr Fact(Context context, SemanticId id) => context.MkBoolConst($"fact:{id}");

    private static SolverAnswer Solve(Context context, IEnumerable<NamedExpression> source, SolverBudget budget, CancellationToken cancellationToken)
    {
        var assertions = source.OrderBy(item => item.Id, StringComparer.Ordinal).ToImmutableArray();
        var canonicalQuery = string.Join("\n", assertions.Select(item => $"{item.Id}={item.Expression}"));
        var digest = $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalQuery))).ToLowerInvariant()}";
        if (cancellationToken.IsCancellationRequested) return Empty(SolverStatus.Cancelled, "cancelled-before-check", digest);
        try
        {
            using var solver = context.MkSolver();
            var parameters = context.MkParams();
            parameters.Add("timeout", budget.TimeoutMilliseconds);
            parameters.Add("rlimit", budget.ResourceLimit);
            solver.Parameters = parameters;
            foreach (var assertion in assertions)
                solver.AssertAndTrack(assertion.Expression, context.MkBoolConst($"assertion:{assertion.Id}"));
            var status = solver.Check();
            if (cancellationToken.IsCancellationRequested) return Empty(SolverStatus.Cancelled, "cancelled-after-check", digest);
            if (status == Status.UNKNOWN) return Empty(SolverStatus.Unknown, solver.ReasonUnknown, digest);
            if (status == Status.UNSATISFIABLE)
            {
                var core = solver.UnsatCore.Select(item => item.FuncDecl.Name.ToString()["assertion:".Length..])
                    .OrderBy(item => item, StringComparer.Ordinal).ToImmutableArray();
                return new SolverAnswer(SolverStatus.Unsat, ImmutableSortedDictionary<string, bool>.Empty, core, null, digest);
            }
            var solverModel = solver.Model;
            var model = solverModel.ConstDecls.Where(item => item.Name.ToString().StartsWith("fact:", StringComparison.Ordinal))
                .ToImmutableSortedDictionary(item => item.Name.ToString()["fact:".Length..], item => solverModel.Evaluate(context.MkConst(item)).IsTrue, StringComparer.Ordinal);
            return new SolverAnswer(SolverStatus.Sat, model, [], null, digest);
        }
        catch (Z3Exception exception)
        {
            return Empty(SolverStatus.Failed, exception.GetType().Name, digest);
        }
    }

    private static SolverAnswer Skipped(SolverAnswer source, string kind) => Empty(source.Status, $"{kind}-not-run", source.QueryDigest);
    private static SolverAnswer Empty(SolverStatus status, string? detail, string digest) => new(status, ImmutableSortedDictionary<string, bool>.Empty, [], detail, digest);
    private static UnsupportedProjectionException Unsupported(SemanticId? id, string code) => new(new SolverDiagnostic(code, "The canonical expression is outside the Truth/And solver projection.", id?.ToString()));
    private sealed record NamedExpression(string Id, BoolExpr Expression);
}
