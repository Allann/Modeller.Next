using Modeller.Output;
using Modeller.Rendering;
using Modeller.Templates;
using Xunit;

namespace Modeller.Conformance.Python.Tests;

/// <summary>
/// Exercises the Child Care Python API pack through the public template-pack loader and validated renderer
/// contract — the same pipeline the CLI uses — rather than only proving provider-level projections in isolation.
/// </summary>
public sealed class PythonApiPackConformanceTests
{
    private static readonly Dictionary<string, string> Parameters = new(StringComparer.Ordinal)
    {
        ["packageName"] = "child_care",
        ["pythonVersion"] = "3.13"
    };

    [Fact]
    public async Task Generation_succeeds_and_covers_entities_enumerations_rules_and_behaviours()
    {
        var result = await GeneratedSourceTreeHarness.GenerateAsync(
            "templates/python/api-project", "child_care", Parameters, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(", ", result.Diagnostics));
        Assert.Contains(result.Files.Keys, path => path.EndsWith("entities/accs_determination_application.py", StringComparison.Ordinal));
        Assert.Contains(result.Files.Keys, path => path.EndsWith("enumerations/booking_status.py", StringComparison.Ordinal));
        Assert.Contains(result.Files.Keys, path => path.EndsWith("rules/determine_accs_eligibility.py", StringComparison.Ordinal));
        Assert.Contains(result.Files.Keys, path => path.EndsWith("behaviours/submit_accs_determination_application.py", StringComparison.Ordinal));

        var rule = result.Files.Single(item => item.Key.EndsWith("rules/determine_accs_eligibility.py", StringComparison.Ordinal)).Value;
        Assert.Contains("class ACCSEligibilityFacts", rule, StringComparison.Ordinal);
        Assert.Contains("active_enrolment_exists: bool", rule, StringComparison.Ordinal);

        var behaviour = result.Files.Single(item => item.Key.EndsWith("behaviours/submit_accs_determination_application.py", StringComparison.Ordinal)).Value;
        Assert.Contains("def submit_accs_determination_application(", behaviour, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repeated_generation_reports_every_artifact_unchanged()
    {
        var first = await GeneratedSourceTreeHarness.GenerateAsync(
            "templates/python/api-project", "child_care", Parameters, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.Success, string.Join(", ", first.Diagnostics));
        Assert.NotEmpty(first.Changes);
        Assert.All(first.Changes, change => Assert.Equal(OutputStatus.Create, change.Status));

        // Re-run against the SAME filesystem, seeded with the first run's ownership manifest — this is what
        // actually exercises OutputApplication's Unchanged detection (matching content + matching manifest
        // digest), rather than merely proving generation is a pure function of its inputs.
        var second = await GeneratedSourceTreeHarness.GenerateAsync(
            "templates/python/api-project", "child_care", Parameters,
            previousManifest: first.Manifest, fileSystem: first.FileSystem, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(second.Success, string.Join(", ", second.Diagnostics));
        Assert.Equal(first.Files.Count, second.Changes.Length);
        Assert.All(second.Changes, change => Assert.Equal(OutputStatus.Unchanged, change.Status));
    }

    [Fact]
    public async Task Unsupported_renderer_version_is_rejected_before_planning()
    {
        var result = await GeneratedSourceTreeHarness.GenerateAsync(
            "templates/python/api-project", "child_care", Parameters,
            renderersOverride: [new RendererIdentity("scriban", "2.0")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("template-pack.renderer.incompatible", result.Diagnostics);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task Unsupported_language_is_rejected_after_a_compatible_renderer_is_validated()
    {
        var result = await GeneratedSourceTreeHarness.GenerateAsync(
            "templates/python/api-project", "child_care", Parameters,
            mutatePackText: text => text.Replace("\"language\": \"python\"", "\"language\": \"cobol\"", StringComparison.Ordinal),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("template-pack.renderer-unsupported", result.Diagnostics);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task A_tampered_template_fails_its_pinned_digest_check()
    {
        var result = await GeneratedSourceTreeHarness.GenerateAsync(
            "templates/python/api-project", "child_care", Parameters,
            mutatePackText: text => text.Replace(
                "\"id\": \"entity\", \"path\": \"entity.py.sbn\", \"digest\": \"sha256:44a7c4e33729cd4c3ec47110bcce1038d1b10b2acf13b0ff1f6d1e839d0ce863\"",
                "\"id\": \"entity\", \"path\": \"entity.py.sbn\", \"digest\": \"sha256:0000000000000000000000000000000000000000000000000000000000000000\"",
                StringComparison.Ordinal),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.StartsWith("template.digest-mismatch", StringComparison.Ordinal));
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task Invalid_python_package_name_is_rejected_with_a_stable_diagnostic()
    {
        var invalid = new Dictionary<string, string>(StringComparer.Ordinal) { ["packageName"] = "Not-A-Valid-Name", ["pythonVersion"] = "3.13" };
        var capability = RendererCapabilityRegistry.Resolve(new RendererIdentity("scriban", "1.0"), "python")!;

        var valid = capability.TryValidateParameters(invalid, out var diagnosticCode);

        Assert.False(valid);
        Assert.Equal("workspace.configuration.python-package-name-invalid", diagnosticCode);
    }

    [Fact]
    public async Task Invalid_python_version_is_rejected_with_a_stable_diagnostic()
    {
        var invalid = new Dictionary<string, string>(StringComparer.Ordinal) { ["packageName"] = "child_care", ["pythonVersion"] = "not-a-version" };
        var capability = RendererCapabilityRegistry.Resolve(new RendererIdentity("scriban", "1.0"), "python")!;

        var valid = capability.TryValidateParameters(invalid, out var diagnosticCode);

        Assert.False(valid);
        Assert.Equal("workspace.configuration.python-version-invalid", diagnosticCode);
    }
}
