using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class AdultDemographicAndReferenceDetailsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private ParseResult? _result;
    private readonly Dictionary<string, string> _values = [];
    private readonly List<string> _ethnicBackgrounds = [];
    private readonly List<string> _languages = [];
    private readonly List<string> _employmentStatuses = [];
    private readonly List<(string Type, string State)> _addresses = [];

    public AdultDemographicAndReferenceDetailsSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = Compile;
    }

    [Given("the adult {string} has the title {string}")]
    public void GivenTitle(string adult, string title) => _values["Title"] = title;

    [Given("the adult has the gender {string}")]
    public void GivenGender(string gender) => _values["Gender"] = gender;

    [Given("the adult has the ethnic backgrounds {string} and {string}")]
    public void GivenEthnicBackgrounds(string first, string second) { _ethnicBackgrounds.Add(first); _ethnicBackgrounds.Add(second); }

    [Given("the adult {string} speaks {string} and {string}")]
    public void GivenLanguages(string adult, string first, string second) { _languages.Add(first); _languages.Add(second); }

    [Given("the adult {string} has a residential address at {string}, {string}, {string}, {string}")]
    public void GivenResidentialAddress(string adult, string line, string suburb, string postcode, string state) => _addresses.Add(("Residential", state));

    [Given("the adult has a postal address at {string}, {string}, {string}, {string}")]
    public void GivenPostalAddress(string line, string suburb, string postcode, string state) => _addresses.Add(("Postal", state));

    [Given("the adult {string} has the employment statuses {string} and {string}")]
    public void GivenEmploymentStatuses(string adult, string first, string second) { _employmentStatuses.Add(first); _employmentStatuses.Add(second); }

    [Given("the adult's highest education received is {string}")]
    public void GivenEducation(string education) => _values["Education"] = education;

    [Given("the adult {string} identifies government-confirmed adult details")]
    public void GivenConfirmedDetails(string adult) => _values["Confirmed"] = adult;

    [Given("those confirmed details have the service identifier {string}")]
    public void GivenServiceIdentifier(string value) => _values["Service identifier"] = value;

    [Given("those confirmed details have the CRN {string}")]
    public void GivenCrn(string value) => _values["CRN"] = value;

    [Given("those confirmed details have the date of birth {string}")]
    public void GivenDateOfBirth(string value) => _values["Date of birth"] = value;

    [Given("an adult with only their existing identity details")]
    public void GivenExistingIdentityOnly() { }

    [When("the adult's demographic details are reviewed")]
    [When("the adult's languages are reviewed")]
    [When("the adult's addresses are reviewed")]
    [When("the adult's work and education details are reviewed")]
    [When("the adult's government-confirmed details are reviewed")]
    public void WhenDetailsAreReviewed() => Compile();

    [Then("the title identifies the reusable title {string}")]
    public void ThenTitle(string value) { Assert.Equal(_values["Title"], value); AssertAdultRelationship("Title", RelationshipCardinality.One); }

    [Then("the gender identifies the reusable gender {string}")]
    public void ThenGender(string value) { Assert.Equal(_values["Gender"], value); AssertAdultRelationship("Gender", RelationshipCardinality.One); }

    [Then("both ethnic backgrounds identify reusable ethnic background entries")]
    public void ThenEthnicBackgrounds() { Assert.Equal(2, _ethnicBackgrounds.Count); AssertAdultRelationship("Ethnic backgrounds", RelationshipCardinality.Many); AssertDescription("Ethnic background"); }

    [Then("none of these details is arbitrary text on the adult")]
    public void ThenNoArbitraryDemographicText() { AssertAdultRelationship("Title", RelationshipCardinality.One); AssertAdultRelationship("Gender", RelationshipCardinality.One); AssertAdultRelationship("Ethnic backgrounds", RelationshipCardinality.Many); }

    [Then("both languages identify reusable language entries")]
    public void ThenLanguages() { Assert.Equal(2, _languages.Count); AssertAdultRelationship("Languages", RelationshipCardinality.Many); }

    [Then("each language remains available for another adult")]
    public void ThenLanguagesReusable() => AssertDescription("Language");

    [Then("both addresses belong to that adult")]
    public void ThenAddressesBelongToAdult() { Assert.Equal(2, _addresses.Count); AssertAdultRelationship("Addresses", RelationshipCardinality.Many); }

    [Then("each address has its selected address type")]
    public void ThenAddressTypesSelected() => _result!.FindEntity("Adult address").AssertField("Address type", type => type is EnumerationDataType, false);

    [Then("each address identifies the reusable state {string}")]
    public void ThenAddressState(string state) { Assert.All(_addresses, address => Assert.Equal(state, address.State)); _result!.FindEntity("Adult address").AssertRelationship("State", RelationshipCardinality.One, false); }

    [Then("the address types are selected from Residential, Commercial, and Postal")]
    public void ThenAddressTypeClosedSet() => Assert.Contains(_addresses, address => address.Type == "Residential" || address.Type == "Postal");

    [Then("both employment statuses identify reusable employment status entries")]
    public void ThenEmploymentStatuses() { Assert.Equal(2, _employmentStatuses.Count); AssertAdultRelationship("Employment statuses", RelationshipCardinality.Many); AssertDescription("Adult employment status"); }

    [Then("the highest education received identifies a reusable education entry")]
    public void ThenEducation() { Assert.NotNull(_values["Education"]); AssertAdultRelationship("Highest education received", RelationshipCardinality.One); AssertDescription("Adult highest education received"); }

    [Then("the confirmed details belong to that adult")]
    public void ThenConfirmedBelongsToAdult() => AssertAdultRelationship("CCSS confirmed adult", RelationshipCardinality.One);

    [Then("the service identifier is {string}")]
    public void ThenServiceIdentifier(string value) { Assert.Equal(_values["Service identifier"], value); AssertConfirmationField("Service identifier", type => type is StringDataType); }

    [Then("the CRN is {string}")]
    public void ThenCrn(string value) { Assert.Equal(_values["CRN"], value); AssertConfirmationField("CRN", type => type is StringDataType); }

    [Then("the date of birth is {string}")]
    public void ThenDateOfBirth(string value) { Assert.Equal(_values["Date of birth"], value); Assert.True(DateOnly.TryParse(value, out _)); AssertConfirmationField("Date of birth", type => type is DateDataType); }

    [Then("the adult has no title, gender, ethnic backgrounds, languages, addresses, employment statuses, highest education received, or government-confirmed adult details")]
    public void ThenAllNewDetailsAreOptional()
    {
        foreach (var name in new[] { "Title", "Gender", "Ethnic backgrounds", "Languages", "Addresses", "Employment statuses", "Highest education received", "CCSS confirmed adult" })
            _result!.FindEntity("Adult").AssertRelationship(name, name is "Ethnic backgrounds" or "Languages" or "Addresses" or "Employment statuses" ? RelationshipCardinality.Many : RelationshipCardinality.One, true);
    }

    private void Compile()
    {
        _result ??= RmlCompiler.Compile([new SourceDocument("adult-details.modeller", Source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
        _context.IsSuccess = _result.IsSuccess;
        _context.FailureSummary = string.Join("; ", _result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
    }

    private void AssertAdultRelationship(string name, RelationshipCardinality cardinality) => _result!.FindEntity("Adult").AssertRelationship(name, cardinality, true);
    private void AssertDescription(string entity) => _result!.FindEntity(entity).AssertField("Description", type => type is StringDataType, false);
    private void AssertConfirmationField(string name, Func<DataType, bool> match) => _result!.FindEntity("CCSS confirmed adult").AssertField(name, match, true);

    private const string Source = """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        enumeration Address type
          member Residential
            value 1
          end
          member Commercial
            value 2
          end
          member Postal
            value 3
          end
        end
        entity State
          field Description
            type string
          end
        end
        entity Title
          field Description
            type string
          end
        end
        entity Gender
          field Description
            type string
          end
        end
        entity Ethnic background
          field Description
            type string
          end
        end
        entity Language
          field Description
            type string
          end
        end
        entity Adult employment status
          field Description
            type string
          end
        end
        entity Adult highest education received
          field Description
            type string
          end
        end
        entity Adult address
          owner "Adult"
          field Address line 1
            type string
          end
          field Address line 2
            type string
            optional
          end
          field Suburb
            type string
          end
          field Postcode
            type string
          end
          field Address type
            type enumeration "Address type"
          end
          relationship State
            target "State"
            cardinality one
          end
        end
        entity CCSS confirmed adult
          owner "Adult"
          field Service identifier
            type string
            optional
          end
          field CRN
            type string
            optional
          end
          field Date of birth
            type date
            optional
          end
        end
        entity Adult
          field First name
            type string
          end
          field Last name
            type string
          end
          relationship Title
            target "Title"
            cardinality one
            optional
          end
          relationship Gender
            target "Gender"
            cardinality one
            optional
          end
          relationship Ethnic backgrounds
            target "Ethnic background"
            cardinality many
            optional
          end
          relationship Languages
            target "Language"
            cardinality many
            optional
          end
          relationship Addresses
            target "Adult address"
            cardinality many
            optional
          end
          relationship Employment statuses
            target "Adult employment status"
            cardinality many
            optional
          end
          relationship Highest education received
            target "Adult highest education received"
            cardinality one
            optional
          end
          relationship CCSS confirmed adult
            target "CCSS confirmed adult"
            cardinality one
            optional
          end
        end
        """;
}
