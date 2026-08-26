using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class EnrolmentRelationshipsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string _child = "Alex Smith";
    private string _centre = "River Street";
    private readonly List<string> _arrangements = [];
    private readonly List<string> _tags = [];
    private string? _payeeAccount;
    private ParseResult? _compileResult;

    public EnrolmentRelationshipsSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = Compile;
    }

    [Given("the child {string} attends the centre {string}")]
    [Given("the child {string} has an enrolment at the centre {string}")]
    public void GivenTheChildHasAnEnrolmentAtTheCentre(string child, string centre) =>
        (_child, _centre) = (child, centre);

    [Given("the child {string} has an enrolment with the arrangement {string}")]
    public void GivenTheChildHasAnEnrolmentWithTheArrangement(string child, string arrangement)
    {
        _child = child;
        _arrangements.Add(arrangement);
    }

    [Given("the enrolment has the arrangements {string} and {string}")]
    public void GivenTheEnrolmentHasTheArrangements(string first, string second) =>
        _arrangements.AddRange([first, second]);

    [Given("the enrolment has the tags {string} and {string}")]
    public void GivenTheEnrolmentHasTheTags(string first, string second) =>
        _tags.AddRange([first, second]);

    [Given("the arrangement is paid by the account {string}")]
    public void GivenTheArrangementIsPaidByTheAccount(string account) => _payeeAccount = account;

    [When("the child's enrolment is recorded")]
    [When("the enrolment is reviewed")]
    [When("the enrolment's arrangements are reviewed")]
    public void WhenTheEnrolmentIsReviewed() => Compile();

    [Then("the enrolment is for the child {string}")]
    public void ThenTheEnrolmentIsForTheChild(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Enrolment", "Child"));

    [Then("the enrolment is owned by the centre {string}")]
    public void ThenTheEnrolmentIsOwnedByTheCentre(string expected)
    {
        var revision = _compileResult!.Package!.AuthoredRevision;
        var enrolment = _compileResult.FindEntity("Enrolment");
        var owner = revision.Definitions.OfType<EntityDefinition>().Single(entity => entity.Id == enrolment.OwnerId);
        Assert.Equal(expected, owner.Name.Value);
    }

    [Then("both arrangements belong to that enrolment")]
    public void ThenBothArrangementsBelongToThatEnrolment() =>
        Assert.Equal(_arrangements, RelationshipTargets("Enrolment", "Arrangements"));

    [Then("both tags describe that enrolment")]
    public void ThenBothTagsDescribeThatEnrolment() =>
        Assert.Equal(_tags, RelationshipTargets("Enrolment", "Tags"));

    [Then("{string} is paid by the account {string}")]
    public void ThenTheArrangementIsPaidByTheAccount(string arrangement, string account)
    {
        Assert.Contains(arrangement, _arrangements);
        Assert.Equal(account, _compileResult!.RelationshipTargetName(arrangement, "Payee"));
    }

    private void Compile()
    {
        _compileResult = RmlCompiler.Compile(
            [new SourceDocument("workspace.rml", BuildSource())],
            ParseOptions.EditorLanguage1,
            TestContext.Current.CancellationToken);
        _context.IsSuccess = _compileResult.IsSuccess;
        _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
    }

    private IReadOnlyList<string> RelationshipTargets(string entityName, string relationshipPrefix)
    {
        var revision = _compileResult!.Package!.AuthoredRevision;
        return _compileResult.FindEntity(entityName).Relationships
            .Where(relationship => relationship.Name.Value.StartsWith(relationshipPrefix, StringComparison.Ordinal))
            .Select(relationship => revision.Definitions.OfType<EntityDefinition>().Single(entity => entity.Id == relationship.TargetId).Name.Value)
            .ToArray();
    }

    private string BuildSource()
    {
        var source = new StringBuilder("rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n")
            .AppendLine($"entity \"{_centre}\"").AppendLine("end")
            .AppendLine($"entity \"{_child}\"").AppendLine("end");
        foreach (var arrangement in _arrangements)
        {
            source.AppendLine($"entity \"{arrangement}\"");
            if (_payeeAccount is not null)
                source.AppendLine("  relationship Payee").AppendLine($"    target \"{_payeeAccount}\"").AppendLine("    cardinality one").AppendLine("  end");
            source.AppendLine("end");
        }
        if (_payeeAccount is not null) source.AppendLine($"entity \"{_payeeAccount}\"").AppendLine("end");
        foreach (var tag in _tags) source.AppendLine($"entity \"{tag}\"").AppendLine("end");
        source.AppendLine("entity Enrolment").AppendLine($"  owner \"{_centre}\"")
            .AppendLine("  relationship Child").AppendLine($"    target \"{_child}\"").AppendLine("    cardinality one").AppendLine("  end");
        for (var index = 0; index < _arrangements.Count; index++)
            source.AppendLine($"  relationship Arrangements {index + 1}").AppendLine($"    target \"{_arrangements[index]}\"").AppendLine("    cardinality many").AppendLine("  end");
        for (var index = 0; index < _tags.Count; index++)
            source.AppendLine($"  relationship Tags {index + 1}").AppendLine($"    target \"{_tags[index]}\"").AppendLine("    cardinality many").AppendLine("  end");
        return source.AppendLine("end").ToString();
    }
}
