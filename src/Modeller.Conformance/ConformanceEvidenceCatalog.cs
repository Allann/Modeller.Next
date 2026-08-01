using System.Collections.Immutable;
using System.Text.Json;

namespace Modeller.Conformance;

public sealed record ConformanceEvidenceCatalog(
    ImmutableArray<string> DiagnosticCodes,
    ImmutableArray<int> SourceDecisions,
    ImmutableArray<string> SupportedFixtureSchemas,
    ImmutableArray<string> ExplanationCriteria,
    ImmutableArray<string> SecurityThreats,
    bool SemanticWaiversPermitted)
{
    public bool ImplementationThresholdReady =>
        Enumerable.Range(16, 7).SequenceEqual(SourceDecisions) &&
        !DiagnosticCodes.IsEmpty &&
        SupportedFixtureSchemas.Contains("1.0") &&
        !ExplanationCriteria.IsEmpty &&
        !SecurityThreats.IsEmpty &&
        !SemanticWaiversPermitted;

    public static ConformanceEvidenceCatalog Load(
        string diagnosticCatalogue,
        string coverageManifest,
        string compatibilityMatrix,
        string explanationRubric,
        string securityThreatInventory)
    {
        var diagnostics = ArrayProperty(diagnosticCatalogue, "diagnostics");
        foreach (var diagnostic in diagnostics)
        {
            _ = RequiredString(diagnostic, "stage");
            _ = RequiredString(diagnostic, "severity");
        }
        var diagnosticCodes = diagnostics
            .Select(item => RequiredString(item, "code"))
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        EnsureUnique(diagnosticCodes, "evidence.diagnostic.duplicate");

        var contracts = ArrayProperty(coverageManifest, "contracts");
        var contractIds = contracts.Select(item => RequiredString(item, "contractId")).ToImmutableArray();
        EnsureUnique(contractIds, "evidence.contract.duplicate");
        foreach (var contract in contracts)
        {
            var status = RequiredString(contract, "status");
            if (status is not ("Planned" or "AdapterPending" or "Executable"))
            {
                throw new ConformanceFixtureException(
                    "evidence.coverage.status-unsupported",
                    $"Coverage status '{status}' is not supported.");
            }

            var fixtures = contract.GetProperty("fixtures").EnumerateArray().ToArray();
            if (status != "Planned" && fixtures.Length == 0)
            {
                throw new ConformanceFixtureException(
                    "evidence.coverage.fixture-required",
                    $"Coverage contract '{RequiredString(contract, "contractId")}' requires a fixture.");
            }
        }
        var sourceDecisions = contracts
            .Select(item => RequiredInt(item, "sourceDecision"))
            .Distinct()
            .Order()
            .ToImmutableArray();

        using var compatibility = JsonDocument.Parse(compatibilityMatrix);
        var supportedFixtureSchemas = compatibility.RootElement
            .GetProperty("support")
            .GetProperty("fixtureSchemas")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        EnsureUnique(supportedFixtureSchemas, "evidence.compatibility.duplicate");

        var explanationCriteria = StringArray(explanationRubric, "criteria");

        using var security = JsonDocument.Parse(securityThreatInventory);
        var threats = security.RootElement
            .GetProperty("threats")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        var semanticWaiversPermitted = security.RootElement
            .GetProperty("semanticWaiversPermitted")
            .GetBoolean();

        return new ConformanceEvidenceCatalog(
            diagnosticCodes,
            sourceDecisions,
            supportedFixtureSchemas,
            explanationCriteria,
            threats,
            semanticWaiversPermitted);
    }

    private static ImmutableArray<JsonElement> ArrayProperty(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty(propertyName)
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToImmutableArray();
    }

    private static ImmutableArray<string> StringArray(string json, string propertyName) =>
        ArrayProperty(json, propertyName)
            .Select(item => item.GetString() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

    private static string RequiredString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ConformanceFixtureException(
                "evidence.required-property",
                $"Evidence property '{propertyName}' is required.");

    private static int RequiredInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : throw new ConformanceFixtureException(
                "evidence.required-property",
                $"Evidence property '{propertyName}' is required.");

    private static void EnsureUnique(ImmutableArray<string> values, string code)
    {
        var duplicate = values
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ConformanceFixtureException(code, $"Evidence value '{duplicate.Key}' is duplicated.");
        }
    }
}
