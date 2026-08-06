namespace Modeller.Initiative;

/// <summary>
/// The machine-readable structured record built from an Initiative's accepted responses and selected
/// interventions. Per issue #86's resolution this record — not a separately-named "Business Problem
/// Brief" — is the durable artifact; <see cref="ToMarkdown"/> is a generated projection of it, not the
/// other way around.
/// </summary>
public sealed record InitiativeStructuredFields(
    string OriginalChangeRequest,
    IReadOnlyList<string> ProblemStatement,
    IReadOnlyList<string> AffectedUsers,
    IReadOnlyList<string> PainPoints,
    IReadOnlyList<string> Outcomes,
    IReadOnlyList<string> SuccessCriteria,
    IReadOnlyList<string> NonGoals,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> Risks,
    IReadOnlyList<SelectedIntervention> SelectedInterventions)
{
    public string ToMarkdown()
    {
        var lines = new List<string>
        {
            "## Original Change Request",
            $"- {OriginalChangeRequest}",
        };

        AppendSection(lines, "Problem Statement", ProblemStatement);
        AppendSection(lines, "Affected Users", AffectedUsers);
        AppendSection(lines, "Pain Points", PainPoints);
        AppendSection(lines, "Outcomes", Outcomes);
        AppendSection(lines, "Success Criteria", SuccessCriteria);
        AppendSection(lines, "Non-Goals", NonGoals);
        AppendSection(lines, "Constraints", Constraints);
        AppendSection(lines, "Assumptions", Assumptions);
        AppendSection(lines, "Open Questions", OpenQuestions);
        AppendSection(lines, "Risks", Risks);

        if (SelectedInterventions.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Selected Interventions");
            foreach (var intervention in SelectedInterventions)
            {
                var handoff = intervention.DesignWorkspaceReference is { } reference
                    ? $" (continues into System Design: {reference})"
                    : intervention.ContinuesToDesignWorkspace
                        ? " (queued for System Design)"
                        : string.Empty;
                lines.Add($"- [{intervention.Type}] {intervention.Description} — {intervention.Rationale}{handoff}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendSection(List<string> lines, string title, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        lines.Add(string.Empty);
        lines.Add($"## {title}");
        lines.AddRange(entries.Select(entry => $"- {entry}"));
    }
}
