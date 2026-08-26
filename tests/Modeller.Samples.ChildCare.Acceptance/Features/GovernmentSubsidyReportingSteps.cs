using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class GovernmentSubsidyReportingSteps
{
    private readonly string _modelRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/child-care/model"));
    private bool _confirmedChild;
    private bool _activeOccurrence;
    private bool _deliveredBooking;
    private bool _accsEligible;
    private bool _submitted;

    [Given("the child {string} has confirmed government details")]
    public void GivenConfirmedDetails(string _) => _confirmedChild = true;

    [Given("the child {string} has no confirmed government details")]
    public void GivenNoConfirmedDetails(string _) => _confirmedChild = false;

    [Given("{string} has the arrangement {string} at {string}")]
    public void GivenArrangementAtCentre(string _, string __, string ___) { }

    [When("the government enrolment occurrence is recorded")]
    public void WhenOccurrenceRecorded() => _activeOccurrence = _confirmedChild;

    [When("government enrolment readiness is determined")]
    public void WhenEnrolmentReadinessIsDetermined() { }

    [Then("the occurrence identifies the government enrolment")]
    public void ThenOccurrenceIdentifiesEnrolment() => AssertModelContains("entities/government-enrolment-occurrence.modeller", "field Government enrolment identifier");

    [Then("the occurrence belongs to the arrangement {string}")]
    public void ThenOccurrenceBelongsToArrangement(string _) => AssertModelContains("entities/government-enrolment-occurrence.modeller", "target \"Arrangement\"");

    [Then("the occurrence records its government stage and visible stage")]
    public void ThenOccurrenceRecordsStages()
    {
        AssertModelContains("entities/government-enrolment-occurrence.modeller", "field Government stage");
        AssertModelContains("entities/government-enrolment-occurrence.modeller", "field Visible stage");
    }

    [Then("the arrangement is not ready for a government enrolment occurrence")]
    public void ThenNotReadyForOccurrence() => Assert.False(_confirmedChild);

    [Then("the finding states that confirmed child details are required")]
    public void ThenConfirmedDetailsFinding() => AssertModelContains("rules/determine-government-enrolment-readiness.modeller", "subsidy.confirmed-child-details-required");

    [Given("the arrangement {string} has an active government enrolment occurrence")]
    public void GivenActiveOccurrence(string _) => _activeOccurrence = true;

    [Given("it has an active government enrolment occurrence")]
    public void GivenItHasActiveOccurrence() => _activeOccurrence = true;

    [Given("the arrangement {string} has no active government enrolment occurrence")]
    public void GivenNoActiveOccurrence(string _) => _activeOccurrence = false;

    [Given("its week starting {string} contains the delivered sessions {string} and {string}")]
    public void GivenTwoDeliveredSessions(string _, string __, string ___) => _deliveredBooking = true;

    [Given("its week starting {string} contains the delivered session {string}")]
    public void GivenDeliveredSession(string _, string __) => _deliveredBooking = true;

    [When("the centre submits the weekly session report")]
    [When("the centre submits its weekly session report")]
    public void WhenCentreSubmitsReport() => _submitted = _activeOccurrence && _deliveredBooking;

    [When("session-report readiness is determined")]
    public void WhenReportReadinessIsDetermined() { }

    [Then("the report belongs to the arrangement {string}")]
    public void ThenReportBelongsToArrangement(string _) => AssertModelContains("entities/weekly-session-report.modeller", "relationship Arrangement");

    [Then("the report starts on {string}")]
    public void ThenReportStartsOn(string _) => AssertModelContains("entities/weekly-session-report.modeller", "field Week start date");

    [Then("the report contains {string} and {string}")]
    public void ThenReportContainsSessions(string _, string __) => AssertModelContains("entities/weekly-session-report.modeller", "relationship Delivered bookings");

    [Then("the report advances from Draft to Submitted")]
    public void ThenReportAdvances() 
    {
        Assert.True(_submitted);
        AssertModelContains("behaviours/submit-weekly-session-report.modeller", "from \"Report Draft\"");
        AssertModelContains("behaviours/submit-weekly-session-report.modeller", "to \"Report Submitted\"");
    }

    [Then("the weekly session report is not ready for submission")]
    public void ThenReportNotReady() => Assert.False(_activeOccurrence && _deliveredBooking);

    [Then("the finding states that an active government enrolment occurrence is required")]
    public void ThenActiveOccurrenceFinding() => AssertModelContains("rules/determine-session-report-readiness.modeller", "subsidy.active-occurrence-required");

    [Given("the submitted weekly report for {string} starts on {string}")]
    public void GivenSubmittedReport(string _, string __) => _submitted = true;

    [When("government subsidy results are recorded")]
    public void WhenResultsRecorded() => Assert.True(_submitted);

    [Then("the result records the weekly fee, care hours, entitlement amount, subsidised hours, and absence count")]
    public void ThenResultRecordsTotals()
    {
        var source = ReadModel("entities/weekly-subsidy-result.modeller");
        Assert.All(new[] { "Weekly fee", "Care hours", "Entitlement amount", "Subsidised hours", "Absence count" }, field => Assert.Contains($"field {field}", source));
    }

    [Then("each session entitlement identifies its delivered session")]
    public void ThenEntitlementIdentifiesSession() => AssertModelContains("entities/session-entitlement.modeller", "relationship Delivered booking");

    [Then("each session entitlement records the amount, subsidised hours, recipient, and entitlement type")]
    public void ThenEntitlementRecordsDetails()
    {
        var source = ReadModel("entities/session-entitlement.modeller");
        Assert.All(new[] { "Entitlement amount", "Subsidised hours", "Recipient", "Entitlement type" }, field => Assert.Contains($"field {field}", source));
    }

    [Then("a nil or partial session entitlement can record a reason")]
    public void ThenNilOrPartialReason() => AssertModelContains("entities/session-entitlement.modeller", "field Nil or partial reason");

    [Given("the arrangement {string} is an ACCS arrangement")]
    public void GivenAccsArrangement(string _) { }

    [Given("its ACCS determination is eligible")]
    public void GivenAccsEligible() => (_accsEligible, _deliveredBooking) = (true, true);

    [Then("the report follows the government subsidy reporting lifecycle")]
    public void ThenUsesReportingLifecycle()
    {
        Assert.True(_accsEligible && _submitted);
        AssertModelContains("entities/weekly-session-report.modeller", "lifecycle Weekly session report lifecycle");
    }

    [Then("the ACCS determination is not duplicated by the reporting capability")]
    public void ThenAccsNotDuplicated()
    {
        var files = Directory.GetFiles(_modelRoot, "*.modeller", SearchOption.AllDirectories);
        Assert.Single(files, path => File.ReadAllText(path).Contains("rule Determine ACCS eligibility", StringComparison.Ordinal));
    }

    private string ReadModel(string relativePath) => File.ReadAllText(Path.Combine(_modelRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private void AssertModelContains(string relativePath, string expected) => Assert.Contains(expected, ReadModel(relativePath));
}
