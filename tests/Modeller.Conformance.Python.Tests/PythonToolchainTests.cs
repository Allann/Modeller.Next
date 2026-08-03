using System.Diagnostics;
using Xunit;

namespace Modeller.Conformance.Python.Tests;

/// <summary>
/// Verifies the generated Python API package with an actual Python interpreter. Locally, these tests skip —
/// never silently pass, never fail the build — when no interpreter (or, for the OpenAPI check, FastAPI/Pydantic)
/// is available. Under CI (detected via the standard <c>CI</c> environment variable), the same gap instead fails
/// the test outright: <c>.github/workflows/dotnet-tests.yml</c> always installs Python and
/// requirements-conformance.txt, so a missing toolchain in CI means the pipeline is misconfigured, not that these
/// checks should be silently unenforced. See README.md for the optional local setup.
/// </summary>
public sealed class PythonToolchainTests
{
    private static readonly Dictionary<string, string> Parameters = new(StringComparer.Ordinal)
    {
        ["packageName"] = "child_care",
        ["pythonVersion"] = "3.13"
    };

    [Fact]
    public async Task Generated_python_api_package_compiles()
    {
        var interpreter = PythonInterpreter.Resolve();
        RequireToolchainOrSkip(interpreter is not null, "No Python interpreter (py/python3/python) was found on PATH.");

        var result = await GeneratedSourceTreeHarness.GenerateAsync(
            "templates/python/api-project", "child_care", Parameters, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join(", ", result.Diagnostics));

        var root = WriteToTempDirectory(result.Files);
        try
        {
            var (exitCode, output) = await RunPythonAsync(interpreter!, ["-m", "compileall", "-q", root], TestContext.Current.CancellationToken);
            Assert.True(exitCode == 0, $"python -m compileall failed:\n{output}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Generated_python_api_package_openapi_schema_builds()
    {
        var interpreter = PythonInterpreter.Resolve();
        RequireToolchainOrSkip(interpreter is not null, "No Python interpreter (py/python3/python) was found on PATH.");

        var (fastApiExitCode, _) = await RunPythonAsync(interpreter!, ["-c", "import fastapi, pydantic"], TestContext.Current.CancellationToken);
        RequireToolchainOrSkip(fastApiExitCode == 0, "fastapi/pydantic are not importable in the resolved interpreter. Run: pip install -r requirements-conformance.txt");

        var result = await GeneratedSourceTreeHarness.GenerateAsync(
            "templates/python/api-project", "child_care", Parameters, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join(", ", result.Diagnostics));

        var root = WriteToTempDirectory(result.Files);
        try
        {
            var srcRoot = Path.Combine(root, "child_care", "src");
            var driver = Path.Combine(root, "print_openapi.py");
            await File.WriteAllTextAsync(driver,
                $"""
                import sys, json
                sys.path.insert(0, {EscapePythonString(srcRoot)})
                from child_care.main import app
                print(json.dumps(app.openapi()))
                """, TestContext.Current.CancellationToken);

            var (exitCode, output) = await RunPythonAsync(interpreter!, [driver], TestContext.Current.CancellationToken);
            Assert.True(exitCode == 0, $"OpenAPI schema build failed:\n{output}");

            using var schema = System.Text.Json.JsonDocument.Parse(output);
            Assert.True(schema.RootElement.TryGetProperty("openapi", out _));
            Assert.True(schema.RootElement.TryGetProperty("paths", out _));
            Assert.True(schema.RootElement.TryGetProperty("info", out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Skips locally; fails outright under CI, where the toolchain is guaranteed by
    /// <c>.github/workflows/dotnet-tests.yml</c> — so a missing toolchain there means the pipeline itself is
    /// broken, and silently skipping would let issue #40's Python acceptance checks go unenforced.
    /// </summary>
    private static void RequireToolchainOrSkip(bool available, string reason)
    {
        if (available) return;
        if (IsContinuousIntegration())
            Assert.Fail($"{reason} This is required under CI — the pipeline should have installed it.");
        Assert.Skip(reason);
    }

    private static bool IsContinuousIntegration() =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private static string EscapePythonString(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string WriteToTempDirectory(IReadOnlyDictionary<string, string> files)
    {
        var root = Path.Combine(Path.GetTempPath(), "modeller-conformance-" + Guid.NewGuid().ToString("N"));
        foreach (var (path, content) in files)
        {
            var target = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, content);
        }
        return root;
    }

    private static async Task<(int ExitCode, string Output)> RunPythonAsync(string interpreter, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(interpreter) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        PythonInterpreter.StripInstrumentationEnvironment(start.Environment);
        using var process = Process.Start(start)!;

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linked.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linked.Token);
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(linked.Token);
            return (process.ExitCode, stdoutTask.Result + stderrTask.Result);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            throw new TimeoutException(
                $"'{interpreter} {string.Join(' ', arguments)}' did not exit within 2 minutes and was killed. " +
                "This usually means a code-coverage collector's instrumentation environment leaked into the child " +
                "process, or the resolved interpreter is a Windows Store app-execution-alias stub.");
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        if (process.HasExited) return;
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Kill() races with the process exiting on its own; only a genuine failure to kill a still-running
            // process (not the exit race) is worth surfacing, since this runs from a timeout handler that must
            // not itself throw a misleading secondary error over the real timeout.
            if (!process.HasExited) throw;
        }
    }
}

internal static class PythonInterpreter
{
    /// <summary>
    /// Environment-variable prefixes used by .NET CLR profilers and test-platform data collectors (code coverage,
    /// blame/hang-dump, IntelliTrace). When a test run is instrumented, these are set on the test-host process and
    /// are inherited by any child process it spawns by default. A spawned Python interpreter neither needs nor
    /// understands them, but a leaked profiler hook can make an external data collector wait on this non-.NET
    /// descendant indefinitely instead of finalizing its session — hanging the whole run. Stripping them keeps the
    /// child process launch identical to an uninstrumented run regardless of how the test host itself was started.
    /// </summary>
    private static readonly string[] InstrumentationEnvironmentPrefixes =
    [
        "COR_", "CORECLR_", "VSTEST_", "MicrosoftInstrumentationEngine_", "COVERAGE_"
    ];

    public static void StripInstrumentationEnvironment(System.Collections.Generic.IDictionary<string, string?> environment)
    {
        foreach (var key in environment.Keys.Where(HasInstrumentationPrefix).ToArray())
        {
            environment.Remove(key);
        }
    }

    private static bool HasInstrumentationPrefix(string key) =>
        InstrumentationEnvironmentPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static readonly Lazy<string?> Resolved = new(Probe);

    public static string? Resolve() => Resolved.Value;

    private static string? Probe()
    {
        foreach (var candidate in new[] { "py", "python3", "python" })
        {
            try
            {
                var start = new ProcessStartInfo(candidate, "--version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                StripInstrumentationEnvironment(start.Environment);
                using var process = Process.Start(start);
                if (process is null) continue;
                process.WaitForExit(5_000);
                if (process.ExitCode == 0) return candidate;
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException)
            {
                // Interpreter not found under this name; try the next candidate.
            }
        }
        return null;
    }
}
