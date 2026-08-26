using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class RoomNicknameSteps
{
    private readonly WorkspaceCompilationContext _context;
    private string? _nickname, _status, _reason, _date, _notes;
    private ParseResult? _result;

    public RoomNicknameSteps(WorkspaceCompilationContext context) { _context = context; _context.Compile = Compile; }
    [Given("a room named {string} with the nickname {string}")] public void GivenNickname(string room, string nickname) => _nickname = nickname;
    [Given("a room named {string} with no nickname")] public void GivenNoNickname(string room) { }
    [Given("the room {string} has an {string} status recorded on {string}")] public void GivenStatusOn(string room, string status, string date) => (_status, _date) = (status, date);
    [Given("the reason is {string}")] public void GivenReason(string reason) => _reason = reason;
    [Given("the room {string} has a {string} status with the notes {string}")] public void GivenStatusNotes(string room, string status, string notes) => (_status, _notes) = (status, notes);
    [Then("the room's nickname is {string}")] public void ThenNickname(string value) { Assert.Equal(_nickname, value); _result!.FindEntity("Room").AssertRelationship("Room nickname", RelationshipCardinality.One, true); }
    [Then("the room has no nickname")] public void ThenNoNickname() { Assert.Null(_nickname); _result!.FindEntity("Room").AssertRelationship("Room nickname", RelationshipCardinality.One, true); }
    [Then("the room's status is {string}")] public void ThenStatus(string value) { Assert.Equal(_status, value); _result!.FindEntity("Room status").AssertField("Status", x => x is EnumerationDataType, false); }
    [Then("the status reason is {string}")] public void ThenReason(string value) { Assert.Equal(_reason, value); _result!.FindEntity("Room status").AssertField("Reason", x => x is StringDataType, false); }
    [Then("the status date is {string}")] public void ThenDate(string value) { Assert.Equal(_date, value); _result!.FindEntity("Room status").AssertField("Date", x => x is DateTimeOffsetDataType, false); }
    [Then("the room's status notes are {string}")] public void ThenNotes(string value) { Assert.Equal(_notes, value); _result!.FindEntity("Room status").AssertField("Notes", x => x is StringDataType, true); }

    private void Compile() { _result = RmlCompiler.Compile([new("workspace.rml", Source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken); _context.IsSuccess = _result.IsSuccess; _context.FailureSummary = string.Join("; ", _result.Diagnostics.Select(x => $"{x.Code}: {x.Message}")); }
    private const string Source = """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        enumeration Room status type
          member Open
            value 1
          end
          member Closed
            value 2
          end
        end
        entity Room nickname
          field Description
            type string
          end
        end
        entity Room status
          field Status
            type enumeration "Room status type"
          end
          field Reason
            type string
          end
          field Date
            type datetimeoffset
          end
          field Notes
            type string
            optional
          end
        end
        entity Room
          relationship Room nickname
            target "Room nickname"
            cardinality one
            optional
          end
          relationship Status
            target "Room status"
            cardinality one
          end
        end
        """;
}
