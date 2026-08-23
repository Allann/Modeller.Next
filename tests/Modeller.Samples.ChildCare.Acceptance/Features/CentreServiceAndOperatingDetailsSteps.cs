using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Compiles a small, self-contained Centre entity carrying the service-offerings and
/// operating-hours relationships, the service-care-type field, an optional Australian Company
/// Number, and a location. The schema is declared statically (every field/relationship always
/// present, mirroring <see cref="AdultIdentityAndContactDetailsSteps"/>) since a Gherkin scenario's
/// literal values are instance data RML does not capture; only the "service offerings include"
/// scenario names more than one item, so that assertion checks captured instance state rather than
/// following a single relationship to a single target.</summary>
[Binding]
public sealed class CentreServiceAndOperatingDetailsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _offering1;
    private string? _offering2;
    private string? _day;
    private string? _openTime;
    private string? _closeTime;
    private string? _serviceCareType;
    private string? _acn;
    private bool _hasLocation;
    private ParseResult? _compileResult;

    public CentreServiceAndOperatingDetailsSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("a centre offering the services {string} and {string}")]
    public void GivenACentreOfferingTheServices(string offering1, string offering2)
    {
        _offering1 = offering1;
        _offering2 = offering2;
    }

    [Given("a centre open on {string} from {string} to {string}")]
    public void GivenACentreOpenOnFromTo(string day, string openTime, string closeTime)
    {
        _day = day;
        _openTime = openTime;
        _closeTime = closeTime;
    }

    [Given("a centre with the service care type {string}, the Australian Company Number {string}, and a location")]
    public void GivenACentreWithTheServiceCareTypeTheAustralianCompanyNumberAndALocation(string serviceCareType, string acn)
    {
        _serviceCareType = serviceCareType;
        _acn = acn;
        _hasLocation = true;
    }

    [Given("a centre with the service care type {string} and no Australian Company Number")]
    public void GivenACentreWithTheServiceCareTypeAndNoAustralianCompanyNumber(string serviceCareType) => _serviceCareType = serviceCareType;

    [Then("the centre's service offerings include {string} and {string}")]
    public void ThenTheCentresServiceOfferingsInclude(string expected1, string expected2)
    {
        Assert.Equal(expected1, _offering1);
        Assert.Equal(expected2, _offering2);
        _compileResult!.FindEntity("Centre").AssertRelationship("Service offerings", RelationshipCardinality.Many, optional: true);
    }

    [Then("the centre's operating hours include {string} from {string} to {string}")]
    public void ThenTheCentresOperatingHoursInclude(string expectedDay, string expectedOpen, string expectedClose)
    {
        Assert.Equal(expectedDay, _day);
        Assert.Equal(expectedOpen, _openTime);
        Assert.Equal(expectedClose, _closeTime);
        Assert.True(TimeOnly.TryParse(expectedOpen, out _), $"'{expectedOpen}' is not a valid time.");
        Assert.True(TimeOnly.TryParse(expectedClose, out _), $"'{expectedClose}' is not a valid time.");
        _compileResult!.FindEntity("Centre").AssertRelationship("Operating hours", RelationshipCardinality.Many, optional: true);
    }

    [Then("the centre's service care type is {string}")]
    public void ThenTheCentresServiceCareTypeIs(string expected)
    {
        Assert.Equal(expected, _serviceCareType);
        _compileResult!.FindEntity("Centre").AssertField("Service care type", type => type is EnumerationDataType, optional: false);
    }

    [Then("the centre's Australian Company Number is {string}")]
    public void ThenTheCentresAustralianCompanyNumberIs(string expected)
    {
        Assert.Equal(expected, _acn);
        _compileResult!.FindEntity("Centre").AssertField("Australian Company Number", type => type is StringDataType, optional: true);
    }

    [Then("the centre has a recorded location")]
    public void ThenTheCentreHasARecordedLocation()
    {
        Assert.True(_hasLocation);
        _compileResult!.FindEntity("Centre").AssertField("Location", type => type is GeographicCoordinateDataType, optional: true);
    }

    [Then("the centre has no Australian Company Number")]
    public void ThenTheCentreHasNoAustralianCompanyNumber()
    {
        Assert.Null(_acn);
        _compileResult!.FindEntity("Centre").AssertField("Australian Company Number", _ => true, optional: true);
    }

    private static string BuildSource() =>
        new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end")
            .AppendLine("enumeration Service care type")
            .AppendLine("  member CBC")
            .AppendLine("    value 1")
            .AppendLine("  end")
            .AppendLine("  member FDC")
            .AppendLine("    value 2")
            .AppendLine("  end")
            .AppendLine("  member OSHC")
            .AppendLine("    value 3")
            .AppendLine("  end")
            .AppendLine("end")
            .AppendLine("enumeration Week day")
            .AppendLine("  member Monday")
            .AppendLine("    value 1")
            .AppendLine("  end")
            .AppendLine("  member Sunday")
            .AppendLine("    value 7")
            .AppendLine("  end")
            .AppendLine("end")
            .AppendLine("entity Service offering")
            .AppendLine("  field Name")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("end")
            .AppendLine("entity Operating hours")
            .AppendLine("  field Day")
            .AppendLine("    type enumeration \"Week day\"")
            .AppendLine("  end")
            .AppendLine("  field Opening time")
            .AppendLine("    type time")
            .AppendLine("  end")
            .AppendLine("  field Closing time")
            .AppendLine("    type time")
            .AppendLine("  end")
            .AppendLine("end")
            .AppendLine("entity Centre")
            .AppendLine("  field Service care type")
            .AppendLine("    type enumeration \"Service care type\"")
            .AppendLine("  end")
            .AppendLine("  field Australian Company Number")
            .AppendLine("    type string")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  field Location")
            .AppendLine("    type coordinate")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  relationship Service offerings")
            .AppendLine("    target \"Service offering\"")
            .AppendLine("    cardinality many")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("  relationship Operating hours")
            .AppendLine("    target \"Operating hours\"")
            .AppendLine("    cardinality many")
            .AppendLine("    optional")
            .AppendLine("  end")
            .AppendLine("end")
            .ToString();
}
