using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Compiles a small, self-contained Centre address shape whose State field is a
/// relationship to a shared State entity rather than free text. The scenario testing two addresses
/// in different states declares two distinct entities ("First centre address" and "Second centre
/// address") since RML describes schema, not per-instance data.</summary>
[Binding]
public sealed class CentreAddressStateSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _state;
    private string? _firstState;
    private string? _secondState;
    private ParseResult? _compileResult;

    public CentreAddressStateSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("a centre address in the suburb {string} with the state {string}")]
    public void GivenACentreAddressInTheSuburbWithTheState(string suburb, string state) => _state = state;

    [Given("a centre address with the state {string} and another centre address with the state {string}")]
    public void GivenACentreAddressWithTheStateAndAnotherCentreAddressWithTheState(string firstState, string secondState)
    {
        _firstState = firstState;
        _secondState = secondState;
    }

    [Then("the centre address's state is {string}")]
    public void ThenTheCentreAddresssStateIs(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Centre address", "State"));

    [Then("the first centre address's state is {string}")]
    public void ThenTheFirstCentreAddresssStateIs(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("First centre address", "State"));

    [Then("the second centre address's state is {string}")]
    public void ThenTheSecondCentreAddresssStateIs(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Second centre address", "State"));

    private string BuildSource()
    {
        var source = new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end");
        if (_state is not null)
        {
            source.AppendLine($"entity {_state}").AppendLine("end")
                .AppendLine("entity Centre address")
                .AppendLine("  relationship State")
                .AppendLine($"    target \"{_state}\"")
                .AppendLine("    cardinality one")
                .AppendLine("  end")
                .AppendLine("end");
        }
        if (_firstState is not null)
        {
            source.AppendLine($"entity {_firstState}").AppendLine("end")
                .AppendLine($"entity {_secondState}").AppendLine("end")
                .AppendLine("entity First centre address")
                .AppendLine("  relationship State")
                .AppendLine($"    target \"{_firstState}\"")
                .AppendLine("    cardinality one")
                .AppendLine("  end")
                .AppendLine("end")
                .AppendLine("entity Second centre address")
                .AppendLine("  relationship State")
                .AppendLine($"    target \"{_secondState}\"")
                .AppendLine("    cardinality one")
                .AppendLine("  end")
                .AppendLine("end");
        }
        return source.ToString();
    }
}
