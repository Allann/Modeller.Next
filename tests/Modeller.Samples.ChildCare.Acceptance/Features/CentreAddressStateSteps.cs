using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class CentreAddressStateSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _state, _firstState, _secondState, _stateCode, _stateName;
    private ParseResult? _result;
    public CentreAddressStateSteps(WorkspaceCompilationContext context) { _context = context; _context.Compile = Compile; }
    [Given("a centre address in the suburb {string} with the state {string}")] public void GivenAddress(string suburb, string state) => _state = state;
    [Given("a centre address with the state {string} and another centre address with the state {string}")] public void GivenAddresses(string first, string second) => (_firstState, _secondState) = (first, second);
    [Given("the shared state {string} has the code {string}")] public void GivenStateCode(string name, string code) => (_stateName, _stateCode) = (name, code);
    [Then("the centre address's state is {string}")] public void ThenState(string value) { Assert.Equal(_state, value); AssertStateRelationship(); }
    [Then("the first centre address's state is {string}")] public void ThenFirstState(string value) { Assert.Equal(_firstState, value); AssertStateRelationship(); }
    [Then("the second centre address's state is {string}")] public void ThenSecondState(string value) { Assert.Equal(_secondState, value); AssertStateRelationship(); }
    [Then("the state code is {string}")] public void ThenStateCode(string value) { Assert.Equal(_stateCode, value); _result!.FindEntity("State").AssertField("State code", x => x is StringDataType, false); }
    [Then("the state name is {string}")] public void ThenStateName(string value) { Assert.Equal(_stateName, value); _result!.FindEntity("State").AssertField("State name", x => x is StringDataType, false); }
    private void AssertStateRelationship() => _result!.FindEntity("Centre address").AssertRelationship("State", RelationshipCardinality.One, false);
    private void Compile() { _result = RmlCompiler.Compile([new("workspace.rml", Source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken); _context.IsSuccess = _result.IsSuccess; _context.FailureSummary = string.Join("; ", _result.Diagnostics.Select(x => $"{x.Code}: {x.Message}")); }
    private const string Source = """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        entity State
          field State code
            type string
          end
          field State name
            type string
          end
        end
        entity Centre address
          relationship State
            target "State"
            cardinality one
          end
        end
        """;
}
