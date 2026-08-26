using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modeller.Cli;
using Modeller.Output;
using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Drives a minimal, self-contained workspace (config, identities, template pack, and
/// one small RML source) through the real CLI generate pipeline entirely in memory — the same
/// technique <c>Modeller.Cli.Tests</c> uses, rather than reading and writing the actual, much
/// larger <c>samples/child-care</c> template pack on disk. It proves the generation pipeline's
/// idempotence for a content-porting change shaped like this story's, without depending on that
/// full pack.</summary>
[Binding]
public sealed class WorkspaceGenerationSteps
{
    private const string EntityTemplate = "namespace ChildCare;\npublic sealed record {{ definition.name }};\n";
    private RecordingCliHost? _host;
    private JsonDocument? _secondDryRun;

    [Given("an enrolment connects a child, a centre, arrangements, tags, and payee accounts")]
    public void GivenAnEnrolmentHasBeenAddedToTheSampleWorkspace()
    {
        const string modelSource = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Centre
            end
            entity Child
            end
            entity Account
            end
            entity Arrangement
              relationship Payee
                target "Account"
                cardinality one
              end
            end
            entity Enrolment tag
              field Description
                type string
              end
            end
            entity Enrolment
              owner "Centre"
              relationship Child
                target "Child"
                cardinality one
              end
              relationship Arrangements
                target "Arrangement"
                cardinality many
              end
              relationship Tags
                target "Enrolment tag"
                cardinality many
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("a family connects its children, related adults, family account, enrolment, and arrangements")]
    public void GivenAFamilyCapabilityHasBeenAddedToTheSampleWorkspace()
    {
        const string modelSource = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Adult
            end
            entity Child
            end
            entity Account
            end
            entity Family account holder
              relationship Adult
                target "Adult"
                cardinality one
              end
              field Account holder rank
                type integer
              end
            end
            entity Family account
              relationship Account
                target "Account"
                cardinality one
              end
              relationship Account holders
                target "Family account holder"
                cardinality many
              end
            end
            entity Related adult
              relationship Adult
                target "Adult"
                cardinality one
              end
            end
            entity Family
              relationship Children
                target "Child"
                cardinality many
              end
              relationship Related adults
                target "Related adult"
                cardinality many
              end
              relationship Family account
                target "Family account"
                cardinality one
              end
            end
            entity Arrangement
              relationship Payee
                target "Account"
                cardinality one
              end
            end
            entity Enrolment
              relationship Child
                target "Child"
                cardinality one
              end
              relationship Family
                target "Family"
                cardinality one
              end
              relationship Arrangements
                target "Arrangement"
                cardinality many
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("a waitlist entry connects a child, a centre, waitlist days, a room, and an end reason")]
    public void GivenAWaitlistEntryHasBeenAddedToTheSampleWorkspace()
    {
        const string modelSource = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Centre
            end
            entity Child
            end
            entity Room
            end
            entity Waitlist end reason
              field Description
                type string
              end
            end
            enumeration Waitlist preference type
              member Required
                value 1
              end
              member Flexible
                value 2
              end
            end
            entity Waitlist day
              owner "Waitlist"
              field Week day
                type string
              end
              field Preference
                type enumeration "Waitlist preference type"
              end
            end
            entity Waitlist
              owner "Centre"
              relationship Child
                target "Child"
                cardinality one
              end
              relationship Days
                target "Waitlist day"
                cardinality many
              end
              relationship Preferred room
                target "Room"
                cardinality one
                optional
              end
              relationship End reason
                target "Waitlist end reason"
                cardinality one
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("confirmed child details, an enrolment occurrence, a weekly session report, and subsidy entitlements")]
    public void GivenGovernmentSubsidyReportingHasBeenAddedToTheSampleWorkspace()
    {
        const string modelSource = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Arrangement
            end
            entity Booking
            end
            entity CCSS confirmed child
            end
            entity Government enrolment occurrence
              relationship Arrangement
                target "Arrangement"
                cardinality one
              end
              relationship Confirmed child details
                target "CCSS confirmed child"
                cardinality one
              end
            end
            entity Session entitlement
              relationship Delivered booking
                target "Booking"
                cardinality one
              end
            end
            entity Weekly session report
              relationship Delivered bookings
                target "Booking"
                cardinality many
              end
            end
            entity Weekly subsidy result
              relationship Session entitlements
                target "Session entitlement"
                cardinality many
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the non-chargeable reason {string} has been added to the sample workspace")]
    public void GivenTheNonChargeableReasonHasBeenAddedToTheSampleWorkspace(string reasonName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Absence reason
              field Description
                type string
              end
            end
            entity {reasonName}
              field Description
                type string
              end
            end
            entity Absence
              relationship Absence reason
                target "Absence reason"
                cardinality one
                optional
              end
              relationship Non chargeable reason
                target "{reasonName}"
                cardinality one
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the arrangement end reason {string} has been added to the sample workspace")]
    public void GivenTheArrangementEndReasonHasBeenAddedToTheSampleWorkspace(string reasonName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Account
              field Account number
                type string
              end
            end
            entity {reasonName}
              field Description
                type string
              end
            end
            entity Arrangement
              relationship Payee
                target "Account"
                cardinality one
              end
              relationship End reason
                target "{reasonName}"
                cardinality one
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the state {string} has been added to the sample workspace")]
    public void GivenTheStateHasBeenAddedToTheSampleWorkspace(string stateName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity {stateName}
              field Description
                type string
              end
            end
            entity Centre address
              relationship State
                target "{stateName}"
                cardinality one
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the service offering {string} has been added to the sample workspace")]
    public void GivenTheServiceOfferingHasBeenAddedToTheSampleWorkspace(string offeringName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity {offeringName}
              field Description
                type string
              end
            end
            entity Centre
              relationship Service offerings
                target "{offeringName}"
                cardinality many
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the community support {string} has been added to the sample workspace")]
    public void GivenTheCommunitySupportHasBeenAddedToTheSampleWorkspace(string supportName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity {supportName}
              field Description
                type string
              end
            end
            entity Child
              relationship Community support
                target "{supportName}"
                cardinality many
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the school {string} has been added to the sample workspace")]
    public void GivenTheSchoolHasBeenAddedToTheSampleWorkspace(string schoolName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity State
              field Description
                type string
              end
            end
            entity School type
              field Description
                type string
              end
            end
            entity {schoolName}
              field Name
                type string
              end
              relationship State
                target "State"
                cardinality one
              end
              relationship School type
                target "School type"
                cardinality one
              end
            end
            entity Child
              relationship School
                target "{schoolName}"
                cardinality one
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the nickname {string} has been added to the sample workspace")]
    public void GivenTheNicknameHasBeenAddedToTheSampleWorkspace(string nicknameName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity {nicknameName}
              field Description
                type string
              end
            end
            entity Room
              relationship Room nickname
                target "{nicknameName}"
                cardinality one
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the adult {string} has been added to the sample workspace")]
    public void GivenTheAdultHasBeenAddedToTheSampleWorkspace(string adultName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            # {adultName}
            entity Adult
              field First name
                type string
              end
              field Last name
                type string
              end
              field Former name
                type string
                optional
              end
              field Date of birth
                type date
                optional
              end
              field CRN
                type string
                optional
              end
              field Home phone
                type string
                optional
              end
              field Mobile phone
                type string
                optional
              end
              field Email
                type string
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("an adult has demographic, language, address, employment, education, and government-confirmed details")]
    public void GivenAdultDemographicAndReferenceDetailsHaveBeenAdded()
    {
        const string modelSource = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            enumeration Address type
              member Residential
                value 1
              end
            end
            entity State
            end
            entity Title
            end
            entity Gender
            end
            entity Ethnic background
            end
            entity Language
            end
            entity Adult employment status
            end
            entity Adult highest education received
            end
            entity Adult address
              relationship State
                target "State"
                cardinality one
              end
              field Address type
                type enumeration "Address type"
              end
            end
            entity CCSS confirmed adult
              field Service identifier
                type string
                optional
              end
            end
            entity Adult
              relationship Title
                target "Title"
                cardinality one
                optional
              end
              relationship Gender
                target "Gender"
                cardinality one
                optional
              end
              relationship Ethnic backgrounds
                target "Ethnic background"
                cardinality many
                optional
              end
              relationship Languages
                target "Language"
                cardinality many
                optional
              end
              relationship Addresses
                target "Adult address"
                cardinality many
                optional
              end
              relationship Employment statuses
                target "Adult employment status"
                cardinality many
                optional
              end
              relationship Highest education received
                target "Adult highest education received"
                cardinality one
                optional
              end
              relationship CCSS confirmed adult
                target "CCSS confirmed adult"
                cardinality one
                optional
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the user {string} has been added to the sample workspace")]
    public void GivenTheUserHasBeenAddedToTheSampleWorkspace(string userName)
    {
        var modelSource = $"""
            rml 1.0
            context Child Care
              version 1.0.0
            end
            # {userName}
            entity User
              field User name
                type string
              end
              field First name
                type string
              end
              field Last name
                type string
              end
              field Authentication source system
                type string
              end
              field Authentication source tenant identifier
                type string
              end
              field Authentication user identifier
                type string
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the bounded workforce and access-control model has been added to the sample workspace")]
    public void GivenTheBoundedWorkforceModelHasBeenAdded()
    {
        const string modelSource = """
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
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the bounded user notification generation model has been added to the sample workspace")]
    public void GivenTheBoundedUserNotificationModelHasBeenAdded()
    {
        const string modelSource = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Organisation
            end
            entity User
            end
            enumeration User notification type
              member User
                value 1
              end
              member Centre
                value 2
              end
              member Provider
                value 3
              end
            end
            enumeration User notification status
              member New
                value 1
              end
              member Viewed
                value 2
              end
              member Completed
                value 3
              end
            end
            entity User notification
              owner "Organisation"
              lifecycle User notification lifecycle
                stage Notification Draft
                stage Notification New
                stage Notification Viewed
                stage Notification Completed
              end
              relationship User
                target "User"
                cardinality one
              end
              field Subject
                type string
              end
              field Description
                type string
              end
              field Url
                type string
                optional
              end
              field Type
                type enumeration "User notification type"
              end
              field Status
                type enumeration "User notification status"
              end
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    [Given("the aggregate ownership audit generation model has been added to the sample workspace")]
    public void GivenTheAggregateOwnershipAuditGenerationModelHasBeenAdded()
    {
        const string modelSource = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Organisation
            end
            entity Centre
              owner "Organisation"
            end
            entity Enrolment
              owner "Centre"
            end
            entity Arrangement
              owner "Enrolment"
            end
            entity Booking
              owner "Enrolment"
            end
            entity Routine booking session
              owner "Arrangement"
            end
            """;
        _host = BuildWorkspaceHost(modelSource);
    }

    /// <summary>Keywords that consume an identity when `RmlCompiler.ApplyIdentities` mints one per
    /// declaration line — kept in sync with `RmlCompiler.IdentityDeclarations` so the number of
    /// identities minted below always matches what the model source actually declares.</summary>
    private static readonly string[] IdentityDeclarationKeywords =
        ["context", "entity", "lifecycle", "stage", "field", "relationship", "enumeration", "member", "fact", "rule", "conclusion", "behaviour", "outcome", "transition", "event", "effect"];

    private static int CountIdentityDeclarations(string modelSource) =>
        modelSource.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Count(line =>
        {
            var trimmed = line.TrimStart();
            var separator = trimmed.IndexOf(' ', StringComparison.Ordinal);
            var keyword = separator < 0 ? trimmed : trimmed[..separator];
            var value = separator < 0 ? string.Empty : trimmed[(separator + 1)..].TrimStart();
            return IdentityDeclarationKeywords.Contains(keyword) && !value.StartsWith('"');
        });

    /// <summary>Assembles the in-memory sample workspace (config, RML source, identities, template
    /// pack, and template body) shared by every scenario that drives generation, minting exactly as
    /// many identities as the model source's declaration lines require.</summary>
    private static RecordingCliHost BuildWorkspaceHost(string modelSource)
    {
        var digest = Digest(EntityTemplate);
        var identityIds = string.Join(", ",
            Enumerable.Range(0, CountIdentityDeclarations(modelSource)).Select(_ => $"\"{Guid.CreateVersion7()}\""));
        return new RecordingCliHost(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["samples/child-care/.modeller/config.json"] = """
                { "version":"1.0", "generationContractVersion":"1.0", "logicalOutputRoot":"generated",
                  "profile":"test", "sources":["model/child-care.modeller"], "templatePack":"templates/pack.json",
                  "parameters":{"projectName":"ChildCare","csharp":{"namespace":"ChildCare","targetFramework":"net10.0"}} }
                """,
            ["samples/child-care/model/child-care.modeller"] = modelSource,
            ["samples/child-care/.modeller/identities.json"] = $$"""
                { "version":"1.0", "documents": { "model/child-care.modeller": [
                {{identityIds}}
                ] } }
                """,
            ["samples/child-care/templates/pack.json"] = $$"""
                { "version":"1.0", "id":"test", "packVersion":"1.0.0", "generationContractVersion":"1.0",
                  "rendererId":"scriban", "rendererVersion":"1.0", "language":"csharp",
                  "templates":[
                    { "id":"entity", "path":"Entity.cs.sbn", "digest":"{{digest}}" }
                  ],
                  "outputs":[
                    { "id":"entity", "scope":"entity", "templateId":"entity", "logicalPath":"Entities/{definitionName}.cs", "owner":"test" }
                  ] }
                """,
            ["samples/child-care/templates/Entity.cs.sbn"] = EntityTemplate
        });
    }

    [When("the workspace is generated")]
    public async Task WhenTheWorkspaceIsGenerated()
    {
        var exit = await CliApplication.RunAsync(
            ["generate", "--workspace", "samples/child-care"], _host!, TestContext.Current.CancellationToken);
        Assert.True(exit == CliExitCode.Success, $"Expected generation success but got {exit}: {_host!.StandardError}");
    }

    [When("the workspace is generated again")]
    public async Task WhenTheWorkspaceIsGeneratedAgain()
    {
        _host!.ClearOutput();
        var exit = await CliApplication.RunAsync(
            ["generate", "--workspace", "samples/child-care", "--dry-run", "--format", "json"], _host!, TestContext.Current.CancellationToken);
        Assert.Equal(CliExitCode.Success, exit);
        _secondDryRun = JsonDocument.Parse(_host!.StandardOutput);
    }

    [Then("the second generation reports every output as unchanged")]
    public void ThenTheSecondGenerationReportsEveryOutputAsUnchanged()
    {
        var changes = _secondDryRun!.RootElement.GetProperty("changes").EnumerateArray().ToArray();
        Assert.NotEmpty(changes);
        Assert.All(changes, change => Assert.Equal("unchanged", change.GetProperty("status").GetString()));
    }

    private static string Digest(string content) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}";

    private sealed class RecordingCliHost(IReadOnlyDictionary<string, string> files) : ICliHost
    {
        private readonly Dictionary<string, string> _files = new(files, StringComparer.Ordinal);
        private readonly StringWriter _output = new();
        private readonly StringWriter _error = new();
        public string StandardOutput => _output.ToString();
        public string StandardError => _error.ToString();
        public TextWriter Output => _output;
        public TextWriter Error => _error;
        public void ClearOutput() => _output.GetStringBuilder().Clear();

        public ValueTask<string> ReadTextAsync(string path, CancellationToken cancellationToken) =>
            _files.TryGetValue(path, out var content)
                ? ValueTask.FromResult(content)
                : ValueTask.FromException<string>(new FileNotFoundException("Source not found.", path));

        public ValueTask WriteTextAsync(string path, string content, bool overwrite, CancellationToken cancellationToken)
        {
            _files[path] = content;
            return ValueTask.CompletedTask;
        }

        public ValueTask ApplyOutputAsync(string root, ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken)
        {
            foreach (var operation in operations)
            {
                var target = $"{root.TrimEnd('/', '\\')}/{operation.Path}";
                if (operation.Kind == FileOperationKind.Delete) _files.Remove(target); else _files[target] = operation.Content!;
            }
            return ValueTask.CompletedTask;
        }

        public bool Exists(string path) => _files.ContainsKey(path);
        public bool IsSymbolicLink(string path) => false;
    }
}
