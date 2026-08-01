using System.Text.Json;
using System.Text.Json.Nodes;
using Modeller.Cli;
using Modeller.Output;
using System.Collections.Immutable;
using Xunit;

namespace Modeller.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task Validate_emits_stable_machine_readable_success_for_child_care_source()
    {
        var source = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"),
            TestContext.Current.CancellationToken);
        var host = new RecordingCliHost(new Dictionary<string, string> { ["child-care.modeller"] = source });

        var exitCode = await CliApplication.RunAsync(
            ["validate", "child-care.modeller", "--format", "json"], host, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Equal("", host.StandardError);
        using var json = JsonDocument.Parse(host.StandardOutput);
        Assert.Equal("1.0", json.RootElement.GetProperty("outputVersion").GetString());
        Assert.True(json.RootElement.GetProperty("valid").GetBoolean());
        Assert.Empty(json.RootElement.GetProperty("diagnostics").EnumerateArray());
    }

    [Fact]
    public async Task Validate_reports_located_semantic_diagnostics_and_a_validation_exit_code()
    {
        var source = await ChildCareSource();
        const string unknown = "0191f6d4-4ea0-7000-8000-00000000ffff";
        source = source.Replace("inputs=0191f6d4-4ea0-7000-8000-000000000006,", $"inputs={unknown},", StringComparison.Ordinal);
        var host = new RecordingCliHost(new Dictionary<string, string> { ["child-care.modeller"] = source });

        var exitCode = await CliApplication.RunAsync(["validate", "child-care.modeller", "--format", "json"], host, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCode.ValidationFailed, exitCode);
        using var json = JsonDocument.Parse(host.StandardOutput);
        var diagnostic = Assert.Single(json.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("validation.reference.fact-unresolved", diagnostic.GetProperty("code").GetString());
        Assert.Equal(9, diagnostic.GetProperty("line").GetInt32());
        Assert.Equal("child-care.modeller", diagnostic.GetProperty("document").GetString());
    }

    [Fact]
    public async Task Pre_cancelled_command_returns_documented_exit_code_without_output_or_reads()
    {
        var host = new RecordingCliHost(new Dictionary<string, string>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exitCode = await CliApplication.RunAsync(["validate", "private.modeller"], host, cancellation.Token);

        Assert.Equal(CliExitCode.Cancelled, exitCode);
        Assert.Equal(0, host.ReadCount);
        Assert.Equal("", host.StandardOutput + host.StandardError);
    }

    [Theory]
    [InlineData("../private.modeller")]
    [InlineData("C:/private.modeller")]
    public async Task Validate_rejects_sources_outside_the_workspace_without_disclosing_the_path(string path)
    {
        var host = new RecordingCliHost(new Dictionary<string, string> { [path] = "secret" });

        var exitCode = await CliApplication.RunAsync(["validate", path], host, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Equal(0, host.ReadCount);
        Assert.DoesNotContain(path, host.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_emits_the_stable_child_care_plan_without_writing_files()
    {
        var host = new RecordingCliHost(new Dictionary<string, string> { ["plan.json"] = ChildCarePlanRequest });

        var exitCode = await CliApplication.RunAsync(
            ["plan", "plan.json", "--format", "json"], host, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Equal(0, host.WriteCount);
        using var json = JsonDocument.Parse(host.StandardOutput);
        Assert.Equal("1.0", json.RootElement.GetProperty("outputVersion").GetString());
        var plan = json.RootElement.GetProperty("plan");
        Assert.Equal("generated", plan.GetProperty("logicalOutputRoot").GetString());
        Assert.Equal(
            ["application/submit-application.cs", "domain/accs-eligibility.cs"],
            plan.GetProperty("artifacts").EnumerateArray().Select(item => item.GetProperty("logicalPath").GetString()));
    }

    [Fact]
    public async Task Root_help_is_generated_without_reading_workspace_files()
    {
        var host = new RecordingCliHost(new Dictionary<string, string>());

        var exitCode = await CliApplication.RunAsync(["--help"], host, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains("validate", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("plan", host.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, host.ReadCount);
    }

    [Fact]
    public async Task Unknown_options_are_rejected_by_the_command_parser_before_workflow_execution()
    {
        var host = new RecordingCliHost(new Dictionary<string, string> { ["child-care.modeller"] = await ChildCareSource() });

        var exitCode = await CliApplication.RunAsync(
            ["validate", "child-care.modeller", "--unknown"], host, TestContext.Current.CancellationToken);

        Assert.Equal(CliExitCode.Usage, exitCode);
        Assert.Equal(0, host.ReadCount);
        Assert.Contains("--unknown", host.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_creates_versioned_configuration_and_does_not_overwrite_without_force()
    {
        var host = new RecordingCliHost(new Dictionary<string, string>());
        Assert.Equal(CliExitCode.Success, await CliApplication.RunAsync(["init"], host, TestContext.Current.CancellationToken));
        Assert.Contains("\"generationContractVersion\": \"1.0\"", host.Files[".modeller/config.json"], StringComparison.Ordinal);
        Assert.Equal(CliExitCode.Configuration, await CliApplication.RunAsync(["init"], host, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Generate_dry_run_previews_child_care_output_without_writes()
    {
        var host = new RecordingCliHost(new Dictionary<string, string> { ["generate.json"] = GenerationRequest() });
        var exit = await CliApplication.RunAsync(["generate", "generate.json", "--dry-run", "--format", "json"], host, TestContext.Current.CancellationToken);
        Assert.Equal(CliExitCode.Success, exit);
        Assert.Equal(0, host.WriteCount);
        using var json = JsonDocument.Parse(host.StandardOutput);
        Assert.True(json.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.All(json.RootElement.GetProperty("changes").EnumerateArray(), item => Assert.Equal("create", item.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Generate_apply_preserves_a_handwritten_collision()
    {
        var host = new RecordingCliHost(new Dictionary<string, string>
        {
            ["generate.json"] = GenerationRequest(),
            ["generated/domain/accs-eligibility.cs"] = "handwritten"
        });
        var exit = await CliApplication.RunAsync(["generate", "generate.json"], host, TestContext.Current.CancellationToken);
        Assert.Equal(CliExitCode.Configuration, exit);
        Assert.Equal("handwritten", host.Files["generated/domain/accs-eligibility.cs"]);
        Assert.Equal(0, host.WriteCount);
    }

    private static async Task<string> ChildCareSource() => await File.ReadAllTextAsync(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"), TestContext.Current.CancellationToken);

    private sealed class RecordingCliHost(IReadOnlyDictionary<string, string> files) : ICliHost
    {
        private readonly Dictionary<string, string> _files = new(files, StringComparer.Ordinal);
        private readonly StringWriter _output = new();
        private readonly StringWriter _error = new();
        public string StandardOutput => _output.ToString();
        public string StandardError => _error.ToString();
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public IReadOnlyDictionary<string, string> Files => _files;
        public TextWriter Output => _output;
        public TextWriter Error => _error;
        public ValueTask<string> ReadTextAsync(string path, CancellationToken cancellationToken)
        {
            ReadCount++;
            return _files.TryGetValue(path, out var content)
                ? ValueTask.FromResult(content)
                : ValueTask.FromException<string>(new FileNotFoundException("Source not found."));
        }
        public ValueTask WriteTextAsync(string path, string content, bool overwrite, CancellationToken cancellationToken)
        {
            WriteCount++;
            if (!overwrite && _files.ContainsKey(path)) return ValueTask.FromException(new IOException("Target exists."));
            _files[path] = content;
            return ValueTask.CompletedTask;
        }
        public ValueTask ApplyOutputAsync(string root, ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken)
        {
            var snapshot = new Dictionary<string, string>(_files, StringComparer.Ordinal);
            foreach (var operation in operations)
            {
                var target = $"{root.TrimEnd('/', '\\')}/{operation.Path}";
                if (operation.Kind == FileOperationKind.Delete) snapshot.Remove(target); else snapshot[target] = operation.Content!;
            }
            _files.Clear(); foreach (var item in snapshot) _files[item.Key] = item.Value;
            WriteCount += operations.Length;
            return ValueTask.CompletedTask;
        }
        public bool Exists(string path) => _files.ContainsKey(path);
    }

    private static string GenerationRequest()
    {
        var planning = JsonNode.Parse(ChildCarePlanRequest)!;
        return new JsonObject
        {
            ["planning"] = planning,
            ["templates"] = new JsonObject
            {
                ["rule.cs"] = new JsonObject { ["digest"] = "sha256:rule-template", ["content"] = "public static class {{ artifact_id }};" },
                ["behaviour.cs"] = new JsonObject { ["digest"] = "sha256:behaviour-template", ["content"] = "public static class {{ artifact_id }};" }
            }
        }.ToJsonString();
    }

    private const string ChildCarePlanRequest = """
        {
          "snapshot": {
            "federation": { "packages": [ {
              "contextId": "0191f6d4-4ea0-7000-8000-000000000001", "slug": "child-care", "contextVersion": "1.0.0",
              "packageDigest": "sha256:package", "semanticDigest": "sha256:context"
            } ] },
            "semanticInputs": [
              { "id": "accs-eligibility", "contextId": "0191f6d4-4ea0-7000-8000-000000000001", "semanticDigest": "sha256:eligibility" },
              { "id": "submit-application", "contextId": "0191f6d4-4ea0-7000-8000-000000000001", "semanticDigest": "sha256:submit" }
            ]
          },
          "configuration": { "profileId": "child-care-csharp", "generationContractVersion": "1.0", "logicalOutputRoot": "generated", "digest": "sha256:configuration" },
          "templatePack": {
            "packId": "csharp-child-care", "packVersion": "1.0.0", "generationContractVersion": "1.0", "digest": "sha256:pack",
            "artifacts": [
              { "artifactId": "submit-application", "templateId": "behaviour.cs", "logicalPath": "application/submit-application.cs", "owner": "child-care", "templateDigest": "sha256:behaviour-template", "semanticInputIds": ["submit-application"] },
              { "artifactId": "accs-eligibility", "templateId": "rule.cs", "logicalPath": "domain/accs-eligibility.cs", "owner": "child-care", "templateDigest": "sha256:rule-template", "semanticInputIds": ["accs-eligibility"] }
            ]
          }
        }
        """;
}
