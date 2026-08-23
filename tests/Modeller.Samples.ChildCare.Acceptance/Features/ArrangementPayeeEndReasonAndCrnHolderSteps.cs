using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Compiles a small, self-contained Arrangement entity carrying a required Payee
/// relationship and optional End reason / CRN holder relationships. Each relationship's target is
/// declared as its own entity, named after the scenario's literal value, so the compiled shape can
/// be checked by following the relationship to its target entity's name (the same technique
/// <see cref="NonChargeableAbsenceReasonSteps"/> uses).</summary>
[Binding]
public sealed class ArrangementPayeeEndReasonAndCrnHolderSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _payeeAccount;
    private string? _endReason;
    private string? _crnHolder;
    private ParseResult? _compileResult;

    public ArrangementPayeeEndReasonAndCrnHolderSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("an arrangement with the account {string} as its payee")]
    public void GivenAnArrangementWithTheAccountAsItsPayee(string account) => _payeeAccount = account;

    [Given("an arrangement that has ended, with the end reason {string}")]
    public void GivenAnArrangementThatHasEndedWithTheEndReason(string endReason)
    {
        _payeeAccount = "Smith family account";
        _endReason = endReason;
    }

    [Given("an arrangement with the adult {string} recorded as its CRN holder")]
    public void GivenAnArrangementWithTheAdultRecordedAsItsCRNHolder(string adult)
    {
        _payeeAccount = "Smith family account";
        _crnHolder = adult;
    }

    [Given("an arrangement with a payee, no end reason, and no CRN holder")]
    public void GivenAnArrangementWithAPayeeNoEndReasonAndNoCRNHolder() => _payeeAccount = "Smith family account";

    [Then("the arrangement's payee is the account {string}")]
    public void ThenTheArrangementsPayeeIsTheAccount(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Arrangement", "Payee"));

    [Then("the arrangement's end reason is {string}")]
    public void ThenTheArrangementsEndReasonIs(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Arrangement", "End reason"));

    [Then("the arrangement's CRN holder is the adult {string}")]
    public void ThenTheArrangementsCRNHolderIsTheAdult(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Arrangement", "CRN holder"));

    [Then("the arrangement has no end reason")]
    public void ThenTheArrangementHasNoEndReason() => AssertNoRelationship("End reason");

    [Then("the arrangement has no CRN holder")]
    public void ThenTheArrangementHasNoCRNHolder() => AssertNoRelationship("CRN holder");

    private void AssertNoRelationship(string relationshipName)
    {
        var arrangement = _compileResult!.FindEntity("Arrangement");
        Assert.DoesNotContain(arrangement.Relationships, relationship => relationship.Name.Value == relationshipName);
    }

    private string BuildSource()
    {
        var source = new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end")
            .AppendLine($"entity {_payeeAccount}")
            .AppendLine("end");
        if (_endReason is not null) source.AppendLine($"entity {_endReason}").AppendLine("end");
        if (_crnHolder is not null) source.AppendLine($"entity {_crnHolder}").AppendLine("end");
        source.AppendLine("entity Arrangement")
            .AppendLine("  relationship Payee")
            .AppendLine($"    target \"{_payeeAccount}\"")
            .AppendLine("    cardinality one")
            .AppendLine("  end");
        if (_endReason is not null)
        {
            source.AppendLine("  relationship End reason")
                .AppendLine($"    target \"{_endReason}\"")
                .AppendLine("    cardinality one")
                .AppendLine("    optional")
                .AppendLine("  end");
        }
        if (_crnHolder is not null)
        {
            source.AppendLine("  relationship CRN holder")
                .AppendLine($"    target \"{_crnHolder}\"")
                .AppendLine("    cardinality one")
                .AppendLine("    optional")
                .AppendLine("  end");
        }
        source.AppendLine("end");
        return source.ToString();
    }
}
