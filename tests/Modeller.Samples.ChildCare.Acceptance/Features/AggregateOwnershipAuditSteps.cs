using Reqnroll;
using Modeller.Model;
using Modeller.Parsing;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class AggregateOwnershipAuditSteps
{
    private readonly WorkspaceCompilationContext _context;
    private static readonly string ModelRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/child-care/model/entities"));
    private static readonly string SampleRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/child-care"));

    public AggregateOwnershipAuditSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = Compile;
    }

    private static readonly IReadOnlyDictionary<string, string> AuditedOwners = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["absence.modeller"] = "Centre",
        ["account.modeller"] = "Organisation",
        ["adult.modeller"] = "Organisation",
        ["adult-address.modeller"] = "Adult",
        ["arrangement.modeller"] = "Enrolment",
        ["arrangement-end-reason.modeller"] = "Organisation",
        ["attendance.modeller"] = "Centre",
        ["booking.modeller"] = "Enrolment",
        ["casual-booking-session.modeller"] = "Arrangement",
        ["centre.modeller"] = "Organisation",
        ["centre-address.modeller"] = "Centre",
        ["centre-operating-hours.modeller"] = "Centre",
        ["centre-service-offering.modeller"] = "Centre",
        ["charge-reason.modeller"] = "Organisation",
        ["charge-type.modeller"] = "Organisation",
        ["child.modeller"] = "Organisation",
        ["child-additional-need.modeller"] = "Child",
        ["child-additional-needs-diagnosed.modeller"] = "Organisation",
        ["child-community-support.modeller"] = "Organisation",
        ["employee.modeller"] = "Organisation",
        ["enrolment.modeller"] = "Centre",
        ["enrolment-tag.modeller"] = "Organisation",
        ["family.modeller"] = "Organisation",
        ["family-account-holder.modeller"] = "Family account",
        ["immunisation-status.modeller"] = "Organisation",
        ["medical-condition.modeller"] = "Organisation",
        ["non-chargeable-reason.modeller"] = "Organisation",
        ["pathway-to-centre.modeller"] = "Organisation",
        ["referral-source.modeller"] = "Organisation",
        ["related-adult.modeller"] = "Family",
        ["related-adult-authorisation.modeller"] = "Organisation",
        ["related-adult-relationship-type.modeller"] = "Organisation",
        ["role.modeller"] = "Organisation",
        ["room.modeller"] = "Centre",
        ["room-age-group.modeller"] = "Room",
        ["room-nickname.modeller"] = "Organisation",
        ["room-session-fee.modeller"] = "Centre",
        ["room-status.modeller"] = "Room",
        ["routine-booking-session.modeller"] = "Arrangement",
        ["school.modeller"] = "Organisation",
        ["service-offering.modeller"] = "Organisation",
        ["session.modeller"] = "Centre",
        ["structure-node.modeller"] = "Organisation",
        ["structure-node-type.modeller"] = "Organisation",
        ["user-notification.modeller"] = "Organisation",
        ["waitlist.modeller"] = "Centre",
        ["waitlist-day.modeller"] = "Waitlist",
        ["waitlist-end-reason.modeller"] = "Organisation"
    };

    [Given("the aggregate ownership audit has been applied to the sample workspace")]
    public void GivenTheAggregateOwnershipAuditHasBeenApplied() { }

    [Then("each audited ported entity declares its supported legacy owner")]
    public void ThenEachAuditedEntityDeclaresItsSupportedOwner()
    {
        foreach (var (fileName, owner) in AuditedOwners)
        {
            var source = File.ReadAllText(Path.Combine(ModelRoot, fileName));
            Assert.Contains($"owner \"{owner}\"", source, StringComparison.Ordinal);
        }
    }

    private void Compile()
    {
        var sourcePaths = Directory.GetFiles(Path.Combine(SampleRoot, "model"), "*.modeller", SearchOption.AllDirectories);
        var sources = sourcePaths
            .Select(path => new SourceDocument(Path.GetRelativePath(Path.Combine(SampleRoot, "model"), path), File.ReadAllText(path)))
            .ToArray();
        var result = RmlCompiler.Compile(sources, ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
        _context.IsSuccess = result.IsSuccess;
        _context.FailureSummary = string.Join("; ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
    }
}
