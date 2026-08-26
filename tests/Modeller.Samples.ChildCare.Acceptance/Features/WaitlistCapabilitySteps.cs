using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class WaitlistCapabilitySteps
{
    private readonly WorkspaceCompilationContext _context;
    private string _child = "Alex Smith";
    private string _centre = "River Street";
    private int _cycleWeek = 1;
    private DateOnly _createdDate = new(2026, 9, 1);
    private DateOnly _preferredStartDate = new(2026, 10, 5);
    private DateOnly? _preferredEndDate;
    private string? _preferredRoom;
    private string? _endReason;
    private readonly List<(string Weekday, string Preference)> _days = [];
    private ParseResult? _compileResult;

    public WaitlistCapabilitySteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = Compile;
    }

    [Given("the child {string} requests care at the centre {string}")]
    public void GivenAChildRequestsCare(string child, string centre) => (_child, _centre) = (child, centre);

    [Given("a child has a waitlist entry at the centre {string}")]
    public void GivenAChildHasAWaitlistEntry(string centre) => _centre = centre;

    [Given("a waitlist entry was created on 1 September 2026")]
    public void GivenCreatedDate() => _createdDate = new(2026, 9, 1);

    [Given("its preferred care period starts on 5 October 2026")]
    public void GivenStartDate() => _preferredStartDate = new(2026, 10, 5);

    [Given("its preferred care period ends on 18 December 2026")]
    public void GivenEndDate() => _preferredEndDate = new(2026, 12, 18);

    [Given("a child has an open waitlist entry")]
    public void GivenOpenWaitlist() => (_preferredEndDate, _endReason) = (null, null);

    [Given("a waitlist entry is for cycle week 2")]
    public void GivenCycleWeekTwo() => _cycleWeek = 2;

    [Given("Monday is required")]
    public void GivenMondayRequired() => _days.Add(("Monday", "Required"));

    [Given("Wednesday is flexible")]
    public void GivenWednesdayFlexible() => _days.Add(("Wednesday", "Flexible"));

    [Given("the preferred room is {string}")]
    public void GivenPreferredRoom(string room) => _preferredRoom = room;

    [Given("the waitlist entry ended because {string}")]
    public void GivenEndReason(string reason) => _endReason = reason;

    [Given("a child has a waitlist entry for a required Monday")]
    public void GivenRequiredMonday() => _days.Add(("Monday", "Required"));

    [When("the waitlist entry is recorded")]
    [When("the waitlist entry is reviewed")]
    [When("the requested pattern of care is reviewed")]
    public void WhenReviewed() => Compile();

    [Then("the waitlist entry is for the child {string}")]
    public void ThenForChild(string expected) => Assert.Equal(expected, _compileResult!.RelationshipTargetName("Waitlist", "Child"));

    [Then("the waitlist entry is owned by the centre {string}")]
    public void ThenOwnedByCentre(string expected) => Assert.Equal(expected, OwnerName("Waitlist"));

    [Then("its creation date is 1 September 2026")]
    public void ThenCreatedDate() => Assert.Equal(new DateOnly(2026, 9, 1), _createdDate);

    [Then("its preferred start date is 5 October 2026")]
    public void ThenStartDate() => Assert.Equal(new DateOnly(2026, 10, 5), _preferredStartDate);

    [Then("its preferred end date is 18 December 2026")]
    public void ThenEndDate() => Assert.Equal(new DateOnly(2026, 12, 18), _preferredEndDate);

    [Then("it has no preferred end date")]
    public void ThenNoEndDate() => Assert.Null(_preferredEndDate);

    [Then("it has no end reason")]
    public void ThenNoEndReason() => Assert.Null(_endReason);

    [Then("the waitlist entry contains a required Monday")]
    public void ThenRequiredMonday()
    {
        Assert.Equal(2, _cycleWeek);
        Assert.Contains(("Monday", "Required"), _days);
    }

    [Then("the waitlist entry contains a flexible Wednesday")]
    public void ThenFlexibleWednesday() => Assert.Contains(("Wednesday", "Flexible"), _days);

    [Then("both waitlist days belong to that waitlist entry")]
    public void ThenDaysOwned() { Assert.Equal(2, _days.Count); Assert.Equal("Waitlist", OwnerName("Waitlist day")); }

    [Then("its preferred room is {string}")]
    public void ThenPreferredRoom(string expected) { Assert.Equal(expected, _preferredRoom); Assert.Equal(expected, _compileResult!.RelationshipTargetName("Waitlist", "Preferred room")); }

    [Then("its end reason is {string}")]
    public void ThenEndReason(string expected) { Assert.Equal(expected, _endReason); Assert.Equal(expected, _compileResult!.RelationshipTargetName("Waitlist", "End reason")); }

    [Then("the waitlist entry does not create a booking")]
    public void ThenNoBooking() => Assert.DoesNotContain(_compileResult!.FindEntity("Waitlist").Relationships, relationship =>
        _compileResult!.Package!.AuthoredRevision.Definitions.OfType<EntityDefinition>().Single(entity => entity.Id == relationship.TargetId).Name.Value is "Booking" or "Session");

    private string OwnerName(string entityName)
    {
        var revision = _compileResult!.Package!.AuthoredRevision;
        var entity = _compileResult.FindEntity(entityName);
        return revision.Definitions.OfType<EntityDefinition>().Single(candidate => candidate.Id == entity.OwnerId).Name.Value;
    }

    private void Compile()
    {
        _compileResult = RmlCompiler.Compile([new SourceDocument("workspace.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
        _context.IsSuccess = _compileResult.IsSuccess;
        _context.FailureSummary = string.Join("; ", _compileResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
    }

    private string BuildSource()
    {
        var source = new StringBuilder("rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n")
            .AppendLine($"entity \"{_centre}\"").AppendLine("end")
            .AppendLine($"entity \"{_child}\"").AppendLine("end")
            .AppendLine("entity Waitlist day").AppendLine("  owner \"Waitlist\"").AppendLine("end");
        if (_preferredRoom is not null) source.AppendLine($"entity \"{_preferredRoom}\"").AppendLine("end");
        if (_endReason is not null) source.AppendLine($"entity \"{_endReason}\"").AppendLine("end");
        source.AppendLine("entity Waitlist").AppendLine($"  owner \"{_centre}\"")
            .AppendLine("  relationship Child").AppendLine($"    target \"{_child}\"").AppendLine("    cardinality one").AppendLine("  end")
            .AppendLine("  relationship Days").AppendLine("    target \"Waitlist day\"").AppendLine("    cardinality many").AppendLine("  end");
        if (_preferredRoom is not null) source.AppendLine("  relationship Preferred room").AppendLine($"    target \"{_preferredRoom}\"").AppendLine("    cardinality one").AppendLine("    optional").AppendLine("  end");
        if (_endReason is not null) source.AppendLine("  relationship End reason").AppendLine($"    target \"{_endReason}\"").AppendLine("    cardinality one").AppendLine("    optional").AppendLine("  end");
        return source.AppendLine("end").ToString();
    }
}
