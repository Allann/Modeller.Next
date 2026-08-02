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
        using var process = Process.Start(start)!;
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, stdout + stderr);
    }
}

internal static class PythonInterpreter
{
    private static readonly Lazy<string?> Resolved = new(Probe);

    public static string? Resolve() => Resolved.Value;

    private static string? Probe()
    {
        foreach (var candidate in new[] { "py", "python3", "python" })
        {
            try
            {
                var start = new ProcessStartInfo(candidate, "--version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
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
