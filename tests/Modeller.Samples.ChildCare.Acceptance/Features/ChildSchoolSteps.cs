using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Compiles a small, self-contained Child entity carrying an optional "School"
/// relationship (targeting an entity named after the scenario's literal school name, the same
/// technique <see cref="NonChargeableAbsenceReasonSteps"/> uses) alongside always-declared,
/// optional Classroom and School start year fields.</summary>
[Binding]
public sealed class ChildSchoolSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _school;
    private string? _classroom;
    private string? _startYear;
    private ParseResult? _compileResult;

    public ChildSchoolSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("a school-aged child at the school {string} in the classroom {string}, starting in {string}")]
    public void GivenASchoolAgedChildAtTheSchoolInTheClassroomStartingIn(string school, string classroom, string startYear)
    {
        _school = school;
        _classroom = classroom;
        _startYear = startYear;
    }

    [Given("a child who is not school-aged, with no school recorded")]
    public void GivenAChildWhoIsNotSchoolAgedWithNoSchoolRecorded()
    {
    }

    [Then("the child's school is {string}")]
    public void ThenTheChildsSchoolIs(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Child", "School"));

    [Then("the child's classroom is {string}")]
    public void ThenTheChildsClassroomIs(string expected)
    {
        Assert.Equal(expected, _classroom);
        _compileResult!.FindEntity("Child").AssertField("Classroom", type => type is StringDataType, optional: true);
    }

    [Then("the child's school start year is {string}")]
    public void ThenTheChildsSchoolStartYearIs(string expected)
    {
        Assert.Equal(expected, _startYear);
        Assert.True(int.TryParse(expected, out _), $"'{expected}' is not a valid year.");
        _compileResult!.FindEntity("Child").AssertField("School start year", type => type is Int32DataType, optional: true);
    }

    [Then("the child has no school")]
    public void ThenTheChildHasNoSchool()
    {
        var child = _compileResult!.FindEntity("Child");
        Assert.DoesNotContain(child.Relationships, relationship => relationship.Name.Value == "School");
    }

    private string BuildSource()
    {
        var source = new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end");
        if (_school is not null) source.AppendLine($"entity {_school}").AppendLine("end");
        source.AppendLine("entity Child")
            .AppendLine("  field Classroom")
            .AppendLine("    type string")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  field School start year")
            .AppendLine("    type int32")
            .AppendLine("    optional")
            .AppendLine("  end");
        if (_school is not null)
        {
            source.AppendLine("  relationship School")
                .AppendLine($"    target \"{_school}\"")
                .AppendLine("    cardinality one")
                .AppendLine("    optional")
                .AppendLine("  end");
        }
        source.AppendLine("end");
        return source.ToString();
    }
}
