using System.Text;
using Modeller.Model;
using Modeller.Parsing;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class WorkforceOrganisationAccessSteps
{
    private readonly WorkspaceCompilationContext _context;
    private readonly HashSet<string> _memberships = ["Harbour Child Care"];
    private string _organisation = "Harbour Child Care";
    private string _roleOrganisation = "Harbour Child Care";
    private string _nodeOrganisation = "Harbour Child Care";
    private string _assignedNode = "Brisbane Centre";
    private string _grantedRight = "attendance_read";
    private DateOnly _startsOn = new(2026, 8, 1);
    private DateOnly? _endsOn;
    private bool _accessAllowed;
    private bool _assignmentValid = true;
    private readonly List<string> _employeeDetails = [];

    public WorkforceOrganisationAccessSteps(WorkspaceCompilationContext context)
    {
        _context = context;
        _context.Compile = Compile;
    }

    [Given("the user {string} is a member of {string} and {string}")]
    public void GivenUserHasTwoMemberships(string _, string first, string second)
    {
        _memberships.Clear();
        _memberships.UnionWith([first, second]);
    }

    [Given("{string} employs the user {string}")]
    public void GivenOrganisationEmploysUser(string organisation, string _) => _organisation = organisation;

    [Given("the employee has external employee identifier {string}")]
    [Given("the employee is named {string}")]
    [Given("the employee has occupation code {string}")]
    [Given("the employee has authentication subject identifier {string}")]
    public void GivenEmployeeDetail(string value) => _employeeDetails.Add(value);

    [Given("{string} has the right {string}")]
    public void GivenOrganisationHasRight(string organisation, string right) => (_organisation, _grantedRight) = (organisation, right);

    [Given("the rights group {string} contains the right {string}")]
    public void GivenRightsGroupContainsRight(string _, string right) => _grantedRight = right;

    [Given("the role {string} contains the rights group {string}")]
    public void GivenRoleContainsRightsGroup(string _, string __) { }

    [Given("the role {string} grants the right {string}")]
    public void GivenRoleGrantsRight(string _, string right) => _grantedRight = right;

    [Given("the role {string} in {string} grants the right {string}")]
    public void GivenRoleInOrganisationGrantsRight(string _, string organisation, string right) =>
        (_organisation, _roleOrganisation, _grantedRight) = (organisation, organisation, right);

    [Given("the user {string} is a member of {string}")]
    public void GivenUserIsMember(string _, string organisation) => _memberships.Add(organisation);

    [Given("the user {string} is a member only of {string}")]
    public void GivenUserIsMemberOnly(string _, string organisation)
    {
        _memberships.Clear();
        _memberships.Add(organisation);
    }

    [Given("the user has the role {string} at structure node {string}")]
    public void GivenRoleAtNode(string _, string node) => _assignedNode = node;

    [Given("the user {string} has the role {string} at structure node {string}")]
    public void GivenNamedUserHasRoleAtNode(string _, string __, string node) => _assignedNode = node;

    [Given("the user has the role {string} at structure node {string} from 1 August 2026")]
    public void GivenCurrentRoleAtNode(string _, string node) => (_assignedNode, _startsOn) = (node, new(2026, 8, 1));

    [Given("the user {string} has the role {string} at structure node {string} from {int} September {int}")]
    public void GivenFutureRoleAtNode(string _, string __, string node, int day, int year) => (_assignedNode, _startsOn) = (node, new(year, 9, day));

    [Given("the user {string} has the role {string} at structure node {string} from {int} August {int}")]
    public void GivenRoleAtNodeFromAugust(string _, string __, string node, int day, int year) => (_assignedNode, _startsOn) = (node, new(year, 8, day));

    [Given("the user {string} had the role {string} at structure node {string} until {int} August {int}")]
    public void GivenEndedRoleAtNode(string _, string __, string node, int day, int year) => (_assignedNode, _endsOn) = (node, new(year, 8, day));

    [Given("the user has the role {string} in {string} at structure node {string}")]
    public void GivenRoleInOrganisationAtNode(string _, string organisation, string node) =>
        (_organisation, _roleOrganisation, _nodeOrganisation, _assignedNode) = (organisation, organisation, organisation, node);

    [Given("the user {string} belongs to {string}")]
    public void GivenUserBelongsTo(string _, string organisation) => _memberships.Add(organisation);

    [Given("the role {string} belongs to {string}")]
    public void GivenRoleBelongsTo(string _, string organisation) => _roleOrganisation = organisation;

    [Given("structure node {string} belongs to {string}")]
    public void GivenNodeBelongsTo(string node, string organisation) => (_assignedNode, _nodeOrganisation) = (node, organisation);

    [When("access to {string} at {string} is decided")]
    public void WhenAccessIsDecided(string right, string node) => Decide(right, node, new(2026, 8, 26));

    [When("access to {string} at {string} is decided on 26 August 2026")]
    public void WhenAccessIsDecidedOnDate(string right, string node) => Decide(right, node, new(2026, 8, 26));

    [When("access to {string} at {string} for {string} is decided")]
    public void WhenAccessForOrganisationIsDecided(string right, string node, string organisation)
    {
        _organisation = organisation;
        Decide(right, node, new(2026, 8, 26));
    }

    [When("that user, role, and structure node are combined in one security assignment")]
    public void WhenCombined() => _assignmentValid = _memberships.Contains(_roleOrganisation) && _roleOrganisation == _nodeOrganisation;

    [Then("the user identifies both organisation memberships")]
    public void ThenUserIdentifiesMemberships()
    {
        Assert.Equal(2, _memberships.Count);
        AssertModelContains("entities/user.modeller", "cardinality many");
    }

    [Then("the employee belongs to {string}")]
    public void ThenEmployeeBelongsTo(string organisation)
    {
        Assert.Equal(organisation, _organisation);
        AssertModelContains("entities/employee.modeller", "owner \"Organisation\"");
    }

    [Then("the employee identifies the user {string}")]
    public void ThenEmployeeIdentifiesUser(string user)
    {
        Assert.Equal(["EMP-1042", user, "EDUCATOR", "subject-1042"], _employeeDetails);
        AssertModelContains("entities/employee.modeller", "relationship User");
    }

    [Then("access is allowed")]
    public void ThenAccessIsAllowed()
    {
        Assert.True(_accessAllowed);
        AssertWorkforceRuleIsFailClosed();
    }

    [Then("access is denied")]
    public void ThenAccessIsDenied()
    {
        Assert.False(_accessAllowed);
        AssertWorkforceRuleIsFailClosed();
    }

    [Then("the security assignment is invalid")]
    public void ThenAssignmentIsInvalid()
    {
        Assert.False(_assignmentValid);
        AssertModelContains("entities/security-assignment.modeller", "owner \"Organisation\"");
        AssertModelContains("entities/security-assignment.modeller", "relationship User");
        AssertModelContains("entities/security-assignment.modeller", "relationship Role");
        AssertModelContains("entities/security-assignment.modeller", "relationship Structure node");
        AssertWorkforceRuleIsFailClosed();
    }

    private void Decide(string right, string node, DateOnly date) =>
        _accessAllowed = _memberships.Contains(_organisation)
            && _organisation == _roleOrganisation
            && _organisation == _nodeOrganisation
            && right == _grantedRight
            && node == _assignedNode
            && _startsOn <= date
            && (_endsOn is null || date <= _endsOn);

    private void Compile()
    {
        var result = RmlCompiler.Compile([new SourceDocument("workforce.rml", BuildSource())], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);
        _context.IsSuccess = result.IsSuccess;
        _context.FailureSummary = string.Join("; ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
    }

    private static string BuildSource() => """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        entity Organisation
        end
        entity User
          relationship Organisations
            target "Organisation"
            cardinality many
            optional
          end
        end
        entity Structure node
          owner "Organisation"
        end
        entity Right
        end
        entity Rights group
          relationship Rights
            target "Right"
            cardinality many
          end
        end
        entity Role
          owner "Organisation"
          relationship Rights groups
            target "Rights group"
            cardinality many
          end
        end
        entity Employee
          owner "Organisation"
          relationship User
            target "User"
            cardinality one
          end
        end
        entity Security assignment
          owner "Organisation"
          relationship User
            target "User"
            cardinality one
          end
          relationship Role
            target "Role"
            cardinality one
          end
          relationship Structure node
            target "Structure node"
            cardinality one
          end
          field Effective start date
            type date
          end
          field Effective end date
            type date
            optional
          end
        end
        """;

    private static void AssertModelContains(string relativePath, string expected)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples/child-care/model"));
        Assert.Contains(expected, File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static void AssertWorkforceRuleIsFailClosed()
    {
        const string relativePath = "rules/determine-workforce-access.modeller";
        AssertModelContains(relativePath, "when all");
        AssertRulePrerequisite(relativePath, "User is an organisation member", "access.organisation-membership-required");
        AssertRulePrerequisite(relativePath, "Security assignment is current", "access.current-assignment-required");
        AssertRulePrerequisite(relativePath, "Security assignment matches exact structure node", "access.exact-structure-node-required");
        AssertRulePrerequisite(relativePath, "Assigned role grants required right", "access.required-right-not-granted");
        AssertRulePrerequisite(relativePath, "Security assignment is organisation consistent", "access.organisation-boundary-violation");
    }

    private static void AssertRulePrerequisite(string relativePath, string fact, string findingCode)
    {
        AssertModelContains(relativePath, $"input \"{fact}\"");
        AssertModelContains(relativePath, $"fact \"{fact}\"");
        AssertModelContains(relativePath, $"finding \"{fact}\" missing {findingCode}");
    }
}
