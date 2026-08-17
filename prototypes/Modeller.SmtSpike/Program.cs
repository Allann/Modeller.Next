using Modeller.Contexts;
using Modeller.Model;

namespace Modeller.SmtSpike;

internal static class Program
{
    private static void Main()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.context-package.v1.json"));
        var package = ContextPackageSystem.Load(bytes).Package!;
        var rule = package.AuthoredRevision.Definitions.OfType<RuleDefinition>().Single();
        var facts = rule.InputFacts.Select(item => item.TargetId).ToArray();
        var answer = SolverProjection.CheckClaim(rule,
            [(facts[0], true, $"fact:{facts[0]}"), (facts[1], true, $"fact:{facts[1]}")], true);

        Console.WriteLine("PROTOTYPE — canonical Truth/And to SMT assurance spike");
        Console.WriteLine($"Translator: {SolverProjection.TranslatorId}");
        Console.WriteLine($"Solver: {SolverProjection.SolverId}");
        Console.WriteLine($"Claim: {answer.Classification}");
        Console.WriteLine($"Consistency: {answer.Consistency.Status}");
        Console.WriteLine($"Claim query: {answer.Claim.Status} {answer.Claim.QueryDigest}");
        Console.WriteLine($"Refutation query: {answer.Refutation.Status}");
    }
}
