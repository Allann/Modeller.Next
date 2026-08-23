using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Compiles a small, self-contained Room entity carrying an optional "Room nickname"
/// relationship targeting an entity named after the scenario's literal nickname (the same
/// technique <see cref="NonChargeableAbsenceReasonSteps"/> uses).</summary>
[Binding]
public sealed class RoomNicknameSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _nickname;
    private ParseResult? _compileResult;

    public RoomNicknameSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = () =>
        {
            _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
            _context.IsSuccess = _compileResult.IsSuccess;
            _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        };
    }

    [Given("a room named {string} with the nickname {string}")]
    public void GivenARoomNamedWithTheNickname(string roomName, string nickname) => _nickname = nickname;

    [Given("a room named {string} with no nickname")]
    public void GivenARoomNamedWithNoNickname(string roomName)
    {
    }

    [Then("the room's nickname is {string}")]
    public void ThenTheRoomsNicknameIs(string expected) =>
        Assert.Equal(expected, _compileResult!.RelationshipTargetName("Room", "Room nickname"));

    [Then("the room has no nickname")]
    public void ThenTheRoomHasNoNickname()
    {
        var room = _compileResult!.FindEntity("Room");
        Assert.DoesNotContain(room.Relationships, relationship => relationship.Name.Value == "Room nickname");
    }

    private string BuildSource()
    {
        var source = new StringBuilder()
            .AppendLine("rml 1.0")
            .AppendLine("context Child Care")
            .AppendLine("  version 1.0.0")
            .AppendLine("end");
        if (_nickname is not null) source.AppendLine($"entity {_nickname}").AppendLine("end");
        source.AppendLine("entity Room");
        if (_nickname is not null)
        {
            source.AppendLine("  relationship Room nickname")
                .AppendLine($"    target \"{_nickname}\"")
                .AppendLine("    cardinality one")
                .AppendLine("    optional")
                .AppendLine("  end");
        }
        source.AppendLine("end");
        return source.ToString();
    }
}
