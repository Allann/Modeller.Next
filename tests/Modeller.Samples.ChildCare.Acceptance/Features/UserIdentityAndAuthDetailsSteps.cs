using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Compiles a small, self-contained User entity mirroring the shape declared in
/// <c>samples/child-care/model/entities/user.modeller</c> (same field names, types, and
/// optionality) and checks that shape against what each scenario asks for. RML describes schema,
/// not instance data, so the literal values in the Gherkin text ("Alex Chen", "alex.chen") are
/// captured as step state and checked for well-formedness against the field's declared type,
/// while the compiled definition itself is checked for the field's presence and optionality —
/// the two things RML actually encodes.</summary>
[Binding]
public sealed class UserIdentityAndAuthDetailsSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _name;
    private string? _userName;
    private string? _authSourceSystem;
    private string? _authTenant;
    private string? _authIdentifier;
    private ParseResult? _compileResult;

    public UserIdentityAndAuthDetailsSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("a user named {string} with the user name {string}")]
    public void GivenAUserNamedWithTheUserName(string name, string userName)
    {
        _name = name;
        _userName = userName;
    }

    [Given("a user authenticated by the source system {string} with tenant {string} and identifier {string}")]
    public void GivenAUserAuthenticatedByTheSourceSystemWithTenantAndIdentifier(string sourceSystem, string tenant, string identifier)
    {
        _authSourceSystem = sourceSystem;
        _authTenant = tenant;
        _authIdentifier = identifier;
    }

    [Then("the user's name is {string}")]
    public void ThenTheUsersNameIs(string expected)
    {
        Assert.Equal(expected, _name);
        AssertField("First name", type => type is StringDataType, optional: false);
        AssertField("Last name", type => type is StringDataType, optional: false);
    }

    [Then("the user's user name is {string}")]
    public void ThenTheUsersUserNameIs(string expected)
    {
        Assert.Equal(expected, _userName);
        AssertField("User name", type => type is StringDataType, optional: false);
    }

    [Then("the user's authentication source system is {string}")]
    public void ThenTheUsersAuthenticationSourceSystemIs(string expected)
    {
        Assert.Equal(expected, _authSourceSystem);
        AssertField("Authentication source system", type => type is StringDataType, optional: false);
    }

    [Then("the user's authentication tenant is {string}")]
    public void ThenTheUsersAuthenticationTenantIs(string expected)
    {
        Assert.Equal(expected, _authTenant);
        AssertField("Authentication source tenant identifier", type => type is StringDataType, optional: false);
    }

    [Then("the user's authentication identifier is {string}")]
    public void ThenTheUsersAuthenticationIdentifierIs(string expected)
    {
        Assert.Equal(expected, _authIdentifier);
        AssertField("Authentication user identifier", type => type is StringDataType, optional: false);
    }

    private void AssertField(string name, Func<DataType, bool> typeMatches, bool optional) =>
        _compileResult!.FindEntity("User").AssertField(name, typeMatches, optional);

    private static string BuildSource() =>
        new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end")
            .AppendLine("entity User")
            .AppendLine("  field User name")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("  field First name")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("  field Last name")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("  field Authentication source system")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("  field Authentication source tenant identifier")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("  field Authentication user identifier")
            .AppendLine("    type string")
            .AppendLine("  end")
            .AppendLine("end")
            .ToString();
}
