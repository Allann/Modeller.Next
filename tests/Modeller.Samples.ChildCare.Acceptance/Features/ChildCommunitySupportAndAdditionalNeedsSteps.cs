using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Compiles a small, self-contained Child entity carrying optional "Community support"
/// and "Support required" relationships, each targeting an entity named after the scenario's
/// literal value (the same technique <see cref="NonChargeableAbsenceReasonSteps"/> uses).</summary>
[Binding]
public sealed class ChildCommunitySupportAndAdditionalNeedsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _communitySupport;
    private string? _supportRequired;
    private ParseResult? _compileResult;

    public ChildCommunitySupportAndAdditionalNeedsSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("a child receiving the community support {string}")]
    public void GivenAChildReceivingTheCommunitySupport(string communitySupport) => _communitySupport = communitySupport;

    [Given("a child requiring the specialised support {string}")]
    public void GivenAChildRequiringTheSpecialisedSupport(string supportRequired) => _supportRequired = supportRequired;

    [Given("a child with no community support and no specialised support required")]
    public void GivenAChildWithNoCommunitySupportAndNoSpecialisedSupportRequired()
    {
    }

    [Then("the child's community support includes {string}")]
    public void ThenTheChildsCommunitySupportIncludes(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Child", "Community support"));

    [Then("the child's specialised support required includes {string}")]
    public void ThenTheChildsSpecialisedSupportRequiredIncludes(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Child", "Support required"));

    [Then("the child has no community support and no specialised support required")]
    public void ThenTheChildHasNoCommunitySupportAndNoSpecialisedSupportRequired()
    {
        var child = _compileResult!.FindEntity("Child");
        Assert.DoesNotContain(child.Relationships, relationship => relationship.Name.Value == "Community support");
        Assert.DoesNotContain(child.Relationships, relationship => relationship.Name.Value == "Support required");
    }

    private string BuildSource()
    {
        var source = new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end");
        if (_communitySupport is not null) source.AppendLine($"entity {_communitySupport}").AppendLine("end");
        if (_supportRequired is not null) source.AppendLine($"entity {_supportRequired}").AppendLine("end");
        source.AppendLine("entity Child");
        if (_communitySupport is not null)
        {
            source.AppendLine("  relationship Community support")
                .AppendLine($"    target \"{_communitySupport}\"")
                .AppendLine("    cardinality many")
                .AppendLine("    optional")
                .AppendLine("  end");
        }
        if (_supportRequired is not null)
        {
            source.AppendLine("  relationship Support required")
                .AppendLine($"    target \"{_supportRequired}\"")
                .AppendLine("    cardinality many")
                .AppendLine("    optional")
                .AppendLine("  end");
        }
        source.AppendLine("end");
        return source.ToString();
    }
}
