using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class CentreServiceAndOperatingDetailsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private readonly List<string> _offerings = [];
    private string? _day, _opening, _closing, _careType, _acn, _latitude, _longitude, _parent, _child, _centreNode;
    private ParseResult? _result;

    public CentreServiceAndOperatingDetailsSteps(WorkspaceCompilationContext context) { _context = context; _context.Compile = Compile; }

    [Given("the service offerings {string} and {string}")] public void GivenOfferings(string first, string second) => _offerings.AddRange([first, second]);
    [Given("a centre offers both services")] public void GivenOffersBoth() { }
    [Given("a centre open on {string} from {string} to {string}")] public void GivenHours(string day, string opening, string closing) => (_day, _opening, _closing) = (day, opening, closing);
    [Given("a centre with the service care type {string}")] public void GivenCareType(string value) => _careType = value;
    [Given("its Australian Company Number is {string}")] public void GivenAcn(string value) => _acn = value;
    [Given("its latitude is {string} and its longitude is {string}")] public void GivenCoordinates(string latitude, string longitude) => (_latitude, _longitude) = (latitude, longitude);
    [Given("a centre with the service care type {string} and no Australian Company Number")] public void GivenNoAcn(string value) => _careType = value;
    [Given("a region named {string} that can contain centres")] public void GivenRegion(string value) => _parent = value;
    [Given("a district named {string} whose parent is {string}")] public void GivenDistrict(string child, string parent) { _child = child; Assert.Equal(_parent, parent); }
    [Given("the centre belongs to {string}")] public void GivenCentreBelongsTo(string value) => _centreNode = value;
    [Given("the centre belongs to the structure node {string}")] public void GivenCentreBelongsToNode(string value) => _centreNode = value;
    [Given("the room {string} belongs to that centre")] public void GivenRoomBelongs(string value) => Assert.NotEmpty(value);

    [Then("the centre's service offerings include {string} and {string}")]
    public void ThenOfferings(string first, string second) { Assert.Contains(first, _offerings); Assert.Contains(second, _offerings); _result!.FindEntity("Centre").AssertRelationship("Service offerings", RelationshipCardinality.Many, false); _result!.FindEntity("Centre service offering").AssertRelationship("Service offering", RelationshipCardinality.One, false); }
    [Then("the centre's operating hours include {string} from {string} to {string}")]
    public void ThenHours(string day, string opening, string closing) { Assert.Equal((_day, _opening, _closing), (day, opening, closing)); var hours = _result!.FindEntity("Centre operating hours"); hours.AssertField("Day", x => x is EnumerationDataType, false); hours.AssertField("Opening time", x => x is TimeDataType, false); hours.AssertField("Closing time", x => x is TimeDataType, false); }
    [Then("the centre's service care type is {string}")] public void ThenCareType(string value) { Assert.Equal(_careType, value); _result!.FindEntity("Centre").AssertField("Service care type", x => x is EnumerationDataType, false); }
    [Then("the centre's Australian Company Number is {string}")] public void ThenAcn(string value) { Assert.Equal(_acn, value); _result!.FindEntity("Centre").AssertField("Australian Company Number", x => x is StringDataType, true); }
    [Then("the centre's latitude is {string} and its longitude is {string}")] public void ThenCoordinates(string latitude, string longitude) { Assert.Equal((_latitude, _longitude), (latitude, longitude)); var centre = _result!.FindEntity("Centre"); centre.AssertField("Latitude", x => x is GeographicCoordinateDataType, false); centre.AssertField("Longitude", x => x is GeographicCoordinateDataType, false); }
    [Then("the centre has no Australian Company Number")] public void ThenNoAcn() { Assert.Null(_acn); _result!.FindEntity("Centre").AssertField("Australian Company Number", _ => true, true); }
    [Then("{string} is the parent of {string}")] public void ThenParent(string parent, string child) { Assert.Equal((_parent, _child), (parent, child)); _result!.FindEntity("Structure node").AssertRelationship("Parent", RelationshipCardinality.One, true); }
    [Then("{string} contains the centre")] public void ThenContainsCentre(string value) { Assert.Equal(_centreNode, value); _result!.FindEntity("Structure node").AssertRelationship("Centres", RelationshipCardinality.Many, true); }
    [Then("the centre's structure nodes include {string}")] public void ThenStructureNodes(string value) { Assert.Equal(_centreNode, value); _result!.FindEntity("Centre").AssertRelationship("Structure nodes", RelationshipCardinality.Many, false); }
    [Then("the centre has no separate direct Rooms relationship")] public void ThenNoRooms() => Assert.DoesNotContain(_result!.FindEntity("Centre").Relationships, x => x.Name.Value == "Rooms");

    private void Compile() { _result = RmlCompiler.Compile([new("workspace.rml", Source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken); _context.IsSuccess = _result.IsSuccess; _context.FailureSummary = string.Join("; ", _result.Diagnostics.Select(x => $"{x.Code}: {x.Message}")); }

    private const string Source = """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        enumeration Service care type
          member CBC
            value 1
          end
          member FDC
            value 2
          end
          member OSHC
            value 3
          end
        end
        enumeration Week day
          member Monday
            value 1
          end
        end
        entity Service offering
          field Name
            type string
          end
          field Description
            type string
          end
        end
        entity Centre service offering
          relationship Service offering
            target "Service offering"
            cardinality one
          end
        end
        entity Centre operating hours
          field Day
            type enumeration "Week day"
          end
          field Opening time
            type time
          end
          field Closing time
            type time
          end
        end
        entity Structure node type
          field Can contain centres
            type boolean
          end
        end
        entity Structure node
          relationship Type
            target "Structure node type"
            cardinality one
          end
          relationship Parent
            target "Structure node"
            cardinality one
            optional
          end
          relationship Centres
            target "Centre"
            cardinality many
            optional
          end
        end
        entity Room
          relationship Centre
            target "Centre"
            cardinality one
          end
        end
        entity Centre
          field Service care type
            type enumeration "Service care type"
          end
          field Australian Company Number
            type string
            optional
          end
          field Latitude
            type coordinate
          end
          field Longitude
            type coordinate
          end
          relationship Service offerings
            target "Centre service offering"
            cardinality many
          end
          relationship Operating hours
            target "Centre operating hours"
            cardinality many
          end
          relationship Structure nodes
            target "Structure node"
            cardinality many
          end
        end
        """;
}
