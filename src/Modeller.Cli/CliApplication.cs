using System.Text.Json;
using System.CommandLine;
using Modeller.Generation;
using Modeller.Parsing;
using Modeller.Rendering;
using Modeller.Output;
using System.Collections.Immutable;
using Modeller.GenerationWorkflow;
using Modeller.Workspace;

namespace Modeller.Cli;

public enum CliExitCode
{
    Success = 0,
    ValidationFailed = 2,
    Usage = 64,
    InputUnavailable = 66,
    Configuration = 78,
    Cancelled = 130
}

public interface ICliHost
{
    TextWriter Output { get; }
    TextWriter Error { get; }
    bool Exists(string path);
    bool IsSymbolicLink(string path);
    ValueTask<string> ReadTextAsync(string path, CancellationToken cancellationToken);
    ValueTask WriteTextAsync(string path, string content, bool overwrite, CancellationToken cancellationToken);
    ValueTask ApplyOutputAsync(string root, ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken);
}

public static class CliApplication
{
    private delegate ValueTask<CliExitCode> Workflow(string path, bool machine, ICliHost host, CancellationToken cancellationToken);
    private static readonly JsonSerializerOptions MachineJson = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public static async ValueTask<CliExitCode> RunAsync(
        IReadOnlyList<string> arguments,
        ICliHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(host);
        if (cancellationToken.IsCancellationRequested) return CliExitCode.Cancelled;
        var command = CommandLine(host);
        var parsed = command.Parse(arguments);
        var configuration = new InvocationConfiguration
        {
            Output = host.Output,
            Error = host.Error,
            EnableDefaultExceptionHandler = false
        };
        var exitCode = await parsed.InvokeAsync(configuration, cancellationToken);
        return parsed.Errors.Count > 0 ? CliExitCode.Usage : (CliExitCode)exitCode;
    }

    private static RootCommand CommandLine(ICliHost host)
    {
        var root = new RootCommand("Model durable domain meaning through stable workflows.");
        root.Subcommands.Add(WorkflowCommand("validate", "Validate readable Modeller source.", host, Validate));
        root.Subcommands.Add(WorkflowCommand("plan", "Create a deterministic generation plan without writing files.", host, Plan));
        root.Subcommands.Add(GenerateCommand(host));
        root.Subcommands.Add(InitCommand(host));
        root.Subcommands.Add(ProjectCommand(host));
        return root;
    }

    private static Command ProjectCommand(ICliHost host)
    {
        var workspace = new Option<string>("--workspace") { Description = "Workspace containing a declared .modeller/config.json.", Required = true };
        var view = new Option<string>("--view") { Description = "Diagram view kind (e.g. Lifecycle, RuleDecision).", Required = true };
        var rootOption = new Option<string?>("--root") { Description = "Semantic id to root the projection at. Omit to list available roots for --view." };
        var format = FormatOption();
        var command = new Command("project", "List projection roots, or project a diagram for one, from a workspace's canonical model.");
        command.Options.Add(workspace); command.Options.Add(view); command.Options.Add(rootOption); command.Options.Add(format);
        command.SetAction(async (parse, cancellation) => (int)await WorkspaceProjection.ExecuteAsync(
            parse.GetRequiredValue(workspace), parse.GetRequiredValue(view), parse.GetValue(rootOption),
            parse.GetValue(format) == "json", host, cancellation));
        return command;
    }

    private static Command GenerateCommand(ICliHost host)
    {
        var request = new Argument<string?>("request") { Description = "Optional package-relative generation workflow request.", Arity = ArgumentArity.ZeroOrOne };
        var workspace = new Option<string?>("--workspace") { Description = "Workspace containing a declared .modeller/config.json." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Preview output without writing files." };
        var format = FormatOption();
        var command = new Command("generate", "Plan, render, and safely preview or apply generated output.");
        command.Arguments.Add(request); command.Options.Add(workspace); command.Options.Add(dryRun); command.Options.Add(format);
        command.Validators.Add(result =>
        {
            if ((result.GetValue(request) is null) == (result.GetValue(workspace) is null))
                result.AddError("Specify either a generation request or --workspace, but not both.");
        });
        command.SetAction(async (parse, cancellation) =>
        {
            var workspacePath = parse.GetValue(workspace);
            return workspacePath is not null
                ? (int)await WorkspaceGeneration.ExecuteAsync(workspacePath, parse.GetValue(dryRun), parse.GetValue(format) == "json", host, cancellation)
                : (int)await Generate(parse.GetValue(request)!, parse.GetValue(dryRun), parse.GetValue(format) == "json", host, cancellation);
        });
        return command;
    }

    private static Command InitCommand(ICliHost host)
    {
        var path = new Option<string>("--path") { DefaultValueFactory = _ => ".modeller/config.json", Description = "Workspace-relative configuration path." };
        var force = new Option<bool>("--force") { Description = "Replace an existing configuration file." };
        var command = new Command("init", "Create a minimal versioned Modeller configuration.");
        command.Options.Add(path); command.Options.Add(force);
        command.SetAction(async (parse, cancellation) => (int)await Init(parse.GetValue(path)!, parse.GetValue(force), host, cancellation));
        return command;
    }

    private static Command WorkflowCommand(
        string name,
        string description,
        ICliHost host,
        Workflow workflow)
    {
        var source = new Argument<string>(name == "validate" ? "source" : "request")
        {
            Description = name == "validate" ? "Package-relative readable source path." : "Package-relative generation request path."
        };
        var format = FormatOption();
        var command = new Command(name, description);
        command.Arguments.Add(source);
        command.Options.Add(format);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetRequiredValue(source);
            var outputFormat = parseResult.GetValue(format) ?? "human";
            return (int)await workflow(path, outputFormat == "json", host, cancellationToken);
        });
        return command;
    }

    private static Option<string> FormatOption()
    {
        var format = new Option<string>("--format")
        {
            Description = "Output format: human or json.",
            DefaultValueFactory = _ => "human"
        };
        format.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string>() is not ("human" or "json"))
                result.AddError("Option '--format' accepts only 'human' or 'json'.");
        });
        return format;
    }

    private static async ValueTask<CliExitCode> Init(string path, bool force, ICliHost host, CancellationToken cancellationToken)
    {
        if (!IsWorkspaceRelative(path)) return CliExitCode.Usage;
        if (host.Exists(path) && !force) { await host.Error.WriteLineAsync("Configuration already exists; use --force to replace it."); return CliExitCode.Configuration; }
        const string content = "{\n  \"version\": \"1.0\",\n  \"generationContractVersion\": \"1.0\",\n  \"logicalOutputRoot\": \"generated\",\n  \"profile\": \"default\"\n}\n";
        try { await host.WriteTextAsync(path, content, force, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return CliExitCode.Cancelled; }
        await host.Output.WriteLineAsync($"Created {path}.");
        return CliExitCode.Success;
    }

    private static async ValueTask<CliExitCode> Generate(string path, bool dryRun, bool machine, ICliHost host, CancellationToken cancellationToken)
    {
        if (!IsWorkspaceRelative(path) || !host.Exists(path)) return CliExitCode.InputUnavailable;
        GenerationWorkflowRequest? request;
        try { request = JsonSerializer.Deserialize<GenerationWorkflowRequest>(await host.ReadTextAsync(path, cancellationToken), MachineJson); }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException) { return CliExitCode.Configuration; }
        if (request is null) return CliExitCode.Configuration;
        var templates = request.Templates.ToImmutableDictionary(x => x.Key, x => new ScribanTemplateSource(x.Value.Digest, x.Value.Content), StringComparer.Ordinal);
        var execution = await GenerationExecution.ExecuteAsync(new(request.Planning, request.Manifest ?? OwnershipManifest.Empty,
            dryRun ? OutputMode.Preview : OutputMode.Apply),
            new ScribanRendererAdapter("scriban/reference", "1.0", templates),
            new CliOutputFileSystem(host, request.Planning.Configuration.LogicalOutputRoot), cancellationToken);
        if (execution.Output is null) return CliExitCode.Configuration;
        var output = execution.Output;
        if (machine) await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", dryRun,
            changes = output.Changes.Select(change => new { change.Path, status = change.Status.ToString().ToLowerInvariant(), change.ArtifactId }),
            diagnostics = output.Diagnostics, manifest = output.Manifest }, MachineJson));
        else foreach (var change in output.Changes) await host.Output.WriteLineAsync($"{change.Status}: {change.Path}");
        return output.IsSuccess ? CliExitCode.Success : CliExitCode.Configuration;
    }

    private static async ValueTask<CliExitCode> Plan(
        string requestPath,
        bool machine,
        ICliHost host,
        CancellationToken cancellationToken)
    {
        if (!IsWorkspaceRelative(requestPath))
        {
            await host.Error.WriteLineAsync("The requested path must remain within the workspace.");
            return CliExitCode.Usage;
        }
        if (!host.Exists(requestPath))
        {
            await host.Error.WriteLineAsync("The requested input could not be read.");
            return CliExitCode.InputUnavailable;
        }

        GenerationPlanningRequest? request;
        try
        {
            var json = await host.ReadTextAsync(requestPath, cancellationToken);
            request = JsonSerializer.Deserialize<GenerationPlanningRequest>(json, MachineJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CliExitCode.Cancelled;
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        {
            await host.Error.WriteLineAsync("The generation planning request is invalid.");
            return CliExitCode.Configuration;
        }

        if (request is null)
        {
            await host.Error.WriteLineAsync("The generation planning request is invalid.");
            return CliExitCode.Configuration;
        }

        var result = GenerationPlanner.Plan(request, cancellationToken);
        if (result.Diagnostics.Any(item => item.Code == "generation.plan.cancelled")) return CliExitCode.Cancelled;
        if (machine)
            await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", plan = result.Plan, diagnostics = result.Diagnostics }, MachineJson));
        else if (result.IsSuccess)
            await host.Output.WriteLineAsync($"Plan: {result.Plan!.Artifacts.Length} artifact(s), no files written.");
        else
            foreach (var diagnostic in result.Diagnostics) await host.Error.WriteLineAsync($"{diagnostic.Code}: {diagnostic.Message}");
        return result.IsSuccess ? CliExitCode.Success : CliExitCode.Configuration;
    }

    private static async ValueTask<CliExitCode> Validate(
        string source,
        bool machine,
        ICliHost host,
        CancellationToken cancellationToken)
    {
        if (!IsWorkspaceRelative(source))
        {
            await host.Error.WriteLineAsync("The requested path must remain within the workspace.");
            return CliExitCode.Usage;
        }

        if (!host.Exists(source))
        {
            await host.Error.WriteLineAsync("The requested source could not be read.");
            return CliExitCode.InputUnavailable;
        }

        string content;
        try
        {
            content = await host.ReadTextAsync(source, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CliExitCode.Cancelled;
        }
        catch (IOException)
        {
            await host.Error.WriteLineAsync("The requested source could not be read.");
            return CliExitCode.InputUnavailable;
        }

        var result = DefinitionParser.Parse([new SourceDocument(Path.GetFileName(source), content)], ParseOptions.Language1, cancellationToken);
        if (result.IsCancelled) return CliExitCode.Cancelled;
        if (machine)
        {
            var payload = new
            {
                outputVersion = "1.0",
                valid = result.IsSuccess,
                diagnostics = result.Diagnostics.Select(item => new
                {
                    code = item.Code,
                    message = item.Message,
                    document = item.Location?.Document,
                    line = item.Location?.Line,
                    column = item.Location?.Column,
                    length = item.Location?.Length
                })
            };
            await host.Output.WriteLineAsync(JsonSerializer.Serialize(payload, MachineJson));
        }
        else if (result.IsSuccess)
        {
            await host.Output.WriteLineAsync("Valid: no diagnostics.");
        }
        else
        {
            foreach (var diagnostic in result.Diagnostics)
                await host.Error.WriteLineAsync($"{diagnostic.Code}: {diagnostic.Message}");
        }

        return result.IsSuccess ? CliExitCode.Success : CliExitCode.ValidationFailed;
    }

    /// <summary>Confined to the workspace per <see cref="LogicalPath"/> — the shared path-confinement primitive; see docs/architecture/decisions/workspace-application-service.mdx.</summary>
    private static bool IsWorkspaceRelative(string path) => LogicalPath.TryCreate(path, out _);
}

public sealed record GenerationTemplateInput(string Digest, string Content);
public sealed record GenerationWorkflowRequest(
    GenerationPlanningRequest Planning,
    IReadOnlyDictionary<string, GenerationTemplateInput> Templates,
    OwnershipManifest? Manifest = null);

internal sealed class CliOutputFileSystem(ICliHost host, string root) : IOutputFileSystem
{
    public async ValueTask<FileObservation> InspectAsync(string path, CancellationToken cancellationToken)
    {
        var target = Target(path);
        return host.Exists(target)
            ? new(true, await host.ReadTextAsync(target, cancellationToken), host.IsSymbolicLink(target))
            : new(false, null, false);
    }

    public async ValueTask ApplyAtomicallyAsync(ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken)
        => await host.ApplyOutputAsync(root, operations, recoveryToken, cancellationToken);

    private string Target(string path) => $"{root.TrimEnd('/', '\\')}/{path.Replace('\\', '/')}";
}
