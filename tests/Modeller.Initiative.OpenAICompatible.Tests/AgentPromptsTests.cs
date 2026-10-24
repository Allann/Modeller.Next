using Modeller.Initiative.OpenAICompatible;
using Xunit;

namespace Modeller.Initiative.OpenAICompatible.Tests;

public class AgentPromptsTests
{
    [Fact]
    public void Website_local_model_uses_a_byte_identical_copy_of_the_prompts()
    {
        var root = RepositoryRoot();
        var canonical = File.ReadAllBytes(Path.Combine(root, "src", "Modeller.Initiative.OpenAICompatible", "agent-prompts.json"));
        var websiteCopy = File.ReadAllBytes(Path.Combine(root, "apps", "website", "src", "lib", "localAgent", "agent-prompts.json"));

        Assert.True(canonical.AsSpan().SequenceEqual(websiteCopy),
            "apps/website/src/lib/localAgent/agent-prompts.json must be an exact copy of " +
            "src/Modeller.Initiative.OpenAICompatible/agent-prompts.json — copy the canonical file over it.");
    }

    [Fact]
    public void Every_task_has_a_system_prompt_that_asks_for_JSON()
    {
        foreach (var prompt in new[] { AgentPrompts.ProposeQuestion, AgentPrompts.DraftFieldUpdate, AgentPrompts.ProposeInterventions, AgentPrompts.EvaluateGate })
        {
            Assert.Contains("Respond only as JSON", string.Join(' ', prompt.System.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)), StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(prompt.User));
        }
    }

    [Fact]
    public void Every_Initiative_field_has_a_description_for_the_question_prompt()
    {
        foreach (var field in Enum.GetValues<InitiativeField>())
            Assert.False(string.IsNullOrWhiteSpace(AgentPrompts.ProposeQuestion.DescribeField(field)), $"{field} has no description.");
        Assert.Contains("{targetFieldDescription}", AgentPrompts.ProposeQuestion.User, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeField_is_empty_for_a_task_without_descriptions()
    {
        Assert.Equal(string.Empty, AgentPrompts.EvaluateGate.DescribeField(InitiativeField.Risks));
    }

    [Fact]
    public void RenderUser_fills_placeholders_once_and_leaves_unknown_ones()
    {
        var template = new AgentPromptTemplate("system", "Field: {field}\nAnswer: {answer}\nOther: {unknown}");

        var rendered = template.RenderUser(new Dictionary<string, string>
        {
            ["field"] = "Risks",
            // A value that looks like a placeholder must come through literally, not be substituted again.
            ["answer"] = "We said {field} twice",
        });

        Assert.Equal("Field: Risks\nAnswer: We said {field} twice\nOther: {unknown}", rendered);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Modeller.slnx"))) return directory.FullName;
        }

        throw new InvalidOperationException("Could not find the repository root (Modeller.slnx).");
    }
}
