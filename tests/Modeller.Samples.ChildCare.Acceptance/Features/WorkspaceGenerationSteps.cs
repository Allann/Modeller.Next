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
        Assert.Equal(CliExitCode.Success, exit);
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
