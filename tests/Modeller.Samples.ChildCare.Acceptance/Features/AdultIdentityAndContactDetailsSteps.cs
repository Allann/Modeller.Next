using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Compiles a small, self-contained Adult entity mirroring the shape declared in
/// <c>samples/child-care/model/entities/adult.modeller</c> (same field names, types, and
/// optionality) and checks that shape against what each scenario asks for. RML describes schema,
/// not instance data, so the literal values in the Gherkin text ("Jane Smith", "1985-04-12") are
/// captured as step state and checked for well-formedness against the field's declared type,
/// while the compiled definition itself is checked for the field's presence and optionality —
/// the two things RML actually encodes.</summary>
[Binding]
public sealed class AdultIdentityAndContactDetailsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _name;
    private string? _dateOfBirth;
    private string? _crn;
    private string? _homePhone;
    private string? _mobilePhone;
    private string? _email;
    private ParseResult? _compileResult;

    public AdultIdentityAndContactDetailsSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("an adult named {string}, born {string}, with the CRN {string}")]
    public void GivenAnAdultNamedBornWithTheCRN(string name, string dateOfBirth, string crn)
    {
        _name = name;
        _dateOfBirth = dateOfBirth;
        _crn = crn;
    }

    [Given("an adult with the home phone {string}, the mobile phone {string}, and the email {string}")]
    public void GivenAnAdultWithContactDetails(string homePhone, string mobilePhone, string email)
    {
        _homePhone = homePhone;
        _mobilePhone = mobilePhone;
        _email = email;
    }

    [Given("an adult with only a name")]
    public void GivenAnAdultWithOnlyAName() => _name = "Jane Smith";

    [Then("the adult's name is {string}")]
    public void ThenTheAdultsNameIs(string expected)
    {
        Assert.Equal(expected, _name);
        AssertField("First name", type => type is StringDataType, optional: false);
        AssertField("Last name", type => type is StringDataType, optional: false);
    }

    [Then("the adult's date of birth is {string}")]
    public void ThenTheAdultsDateOfBirthIs(string expected)
    {
        Assert.Equal(expected, _dateOfBirth);
        Assert.True(DateOnly.TryParse(expected, out _), $"'{expected}' is not a valid date.");
        AssertField("Date of birth", type => type is DateDataType, optional: true);
    }

    [Then("the adult's CRN is {string}")]
    public void ThenTheAdultsCRNIs(string expected)
    {
        Assert.Equal(expected, _crn);
        AssertField("CRN", type => type is StringDataType, optional: true);
    }

    [Then("the adult's home phone is {string}")]
    public void ThenTheAdultsHomePhoneIs(string expected)
    {
        Assert.Equal(expected, _homePhone);
        AssertField("Home phone", type => type is StringDataType, optional: true);
    }

    [Then("the adult's mobile phone is {string}")]
    public void ThenTheAdultsMobilePhoneIs(string expected)
    {
        Assert.Equal(expected, _mobilePhone);
        AssertField("Mobile phone", type => type is StringDataType, optional: true);
    }

    [Then("the adult's email is {string}")]
    public void ThenTheAdultsEmailIs(string expected)
    {
        Assert.Equal(expected, _email);
        AssertField("Email", type => type is StringDataType, optional: true);
    }

    [Then("the adult has no date of birth, no CRN, and no contact details")]
    public void ThenTheAdultHasNoDateOfBirthNoCRNAndNoContactDetails()
    {
        Assert.Null(_dateOfBirth);
        Assert.Null(_crn);
        Assert.Null(_homePhone);
        Assert.Null(_mobilePhone);
        Assert.Null(_email);
        foreach (var fieldName in new[] { "Date of birth", "CRN", "Home phone", "Mobile phone", "Email" })
            AssertField(fieldName, _ => true, optional: true);
    }

    private void AssertField(string name, Func<DataType, bool> typeMatches, bool optional) =>
        _compileResult!.FindEntity("Adult").AssertField(name, typeMatches, optional);

    private static string BuildSource() =>
        new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end")
            .AppendLine("entity Adult")
            .AppendLine("  field First name")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("  field Last name")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("  field Former name")
            .AppendLine("    type string")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  field Date of birth")
            .AppendLine("    type date")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  field CRN")
            .AppendLine("    type string")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  field Home phone")
            .AppendLine("    type string")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  field Mobile phone")
            .AppendLine("    type string")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  field Email")
            .AppendLine("    type string")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("end")
            .ToString();
}
