using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class NonChargeableAbsenceReasonSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _nonChargeableReason;
    private string? _absenceReason;
    private ParseResult? _compileResult;

    public NonChargeableAbsenceReasonSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("an absence for a booked session, marked non-chargeable, with the non-chargeable reason {string} and the absence reason {string}")]
    public void GivenAnAbsenceMarkedNonChargeableWithBothReasons(string nonChargeableReason, string absenceReason)
    {
        _nonChargeableReason = nonChargeableReason;
        _absenceReason = absenceReason;
    }

    [Given("an absence for a booked session that is not marked non-chargeable and carries no non-chargeable reason")]
    public void GivenAnAbsenceWithNoNonChargeableReason()
    {
    }

    [Then("the absence's non-chargeable reason is {string}")]
    public void ThenTheAbsencesNonChargeableReasonIs(string expected)
    {
        var target = RelationshipTargetName(_compileResult!, "Non chargeable reason");
        Assert.Equal(expected, target);
    }

    [Then("the absence's absence reason is {string}")]
    public void ThenTheAbsencesAbsenceReasonIs(string expected)
    {
        var target = RelationshipTargetName(_compileResult!, "Absence reason");
        Assert.Equal(expected, target);
    }

    [Then("the absence has no non-chargeable reason")]
    public void ThenTheAbsenceHasNoNonChargeableReason()
    {
        var absence = FindAbsence(_compileResult!);
        Assert.DoesNotContain(absence.Relationships, relationship => relationship.Name.Value == "Non chargeable reason");
    }

    private static EntityDefinition FindAbsence(ParseResult result) =>
        result.Package!.AuthoredRevision.Definitions.OfType<EntityDefinition>().Single(entity => entity.Name.Value == "Absence");

    private static string RelationshipTargetName(ParseResult result, string relationshipName)
    {
        var revision = result.Package!.AuthoredRevision;
        var absence = FindAbsence(result);
        var relationship = absence.Relationships.Single(item => item.Name.Value == relationshipName);
        var target = revision.Definitions.OfType<EntityDefinition>().Single(entity => entity.Id == relationship.TargetId);
        return target.Name.Value;
    }

    private string BuildSource()
    {
        var source = new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end");
        if (_nonChargeableReason is not null)
            source.AppendLine($"entity {_nonChargeableReason}").AppendLine("end");
        if (_absenceReason is not null)
            source.AppendLine($"entity {_absenceReason}").AppendLine("end");
        source.AppendLine("entity Absence");
        if (_nonChargeableReason is not null)
        {
            source.AppendLine("  relationship Non chargeable reason")
                .AppendLine($"    target \"{_nonChargeableReason}\"")
                .AppendLine("    cardinality one")
                .AppendLine("    optional")
                .AppendLine("  end");
        }
        if (_absenceReason is not null)
        {
            source.AppendLine("  relationship Absence reason")
                .AppendLine($"    target \"{_absenceReason}\"")
                .AppendLine("    cardinality one")
                .AppendLine("    optional")
                .AppendLine("  end");
        }
        source.AppendLine("end");
        return source.ToString();
    }
}
