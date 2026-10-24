using System.Text.Json;
using System.Text.RegularExpressions;

namespace Modeller.Initiative.OpenAICompatible;

/// <summary>One Agent Participant task's prompt pair: a fixed system prompt and a user-prompt template.
/// <see cref="FieldDescriptions"/> (question task only) says in plain words what each Initiative field
/// collects; small local models need that next to the target field to ask about the right thing.</summary>
public sealed record AgentPromptTemplate(string System, string User, IReadOnlyDictionary<string, string>? FieldDescriptions = null)
{
    /// <summary>The description of <paramref name="field"/>, or an empty string when there is none.</summary>
    public string DescribeField(InitiativeField field) =>
        FieldDescriptions is not null && FieldDescriptions.TryGetValue(field.ToString(), out var description) ? description : string.Empty;

    /// <summary>
    /// Fills <c>{name}</c> placeholders in <see cref="User"/> in a single pass, so a value that itself
    /// contains <c>{…}</c> text (a Domain Expert answer, for example) is never substituted again.
    /// Unknown placeholders are left as they are.
    /// </summary>
    public string RenderUser(IReadOnlyDictionary<string, string> values) =>
        AgentPrompts.Placeholder().Replace(User, match =>
            values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
}

/// <summary>
/// The Agent Participant prompts, loaded from the embedded <c>agent-prompts.json</c>. The website's
/// in-browser local model (apps/website/src/lib/localAgent) runs the same tasks from a byte-identical
/// copy of that file — <c>AgentPromptsTests</c> fails if the two drift apart — so a hosted model and a
/// local model are always asked the same thing.
/// </summary>
public static partial class AgentPrompts
{
    private static readonly Lazy<IReadOnlyDictionary<string, AgentPromptTemplate>> All = new(Load);

    public static AgentPromptTemplate ProposeQuestion => All.Value["proposeQuestion"];
    public static AgentPromptTemplate DraftFieldUpdate => All.Value["draftFieldUpdate"];
    public static AgentPromptTemplate ProposeInterventions => All.Value["proposeInterventions"];
    public static AgentPromptTemplate EvaluateGate => All.Value["evaluateGate"];

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.CultureInvariant)]
    internal static partial Regex Placeholder();

    private static IReadOnlyDictionary<string, AgentPromptTemplate> Load()
    {
        using var stream = typeof(AgentPrompts).Assembly.GetManifestResourceStream("Modeller.Initiative.OpenAICompatible.agent-prompts.json")
            ?? throw new InvalidOperationException("The embedded agent-prompts.json resource is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, AgentPromptTemplate>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The embedded agent-prompts.json resource is empty.");
    }
}
