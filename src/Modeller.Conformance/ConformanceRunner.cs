using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Modeller.Conformance;

public interface IConformanceAdapter
{
    string Capability { get; }
    string ContractVersion { get; }

    ValueTask<JsonElement> ExecuteAsync(
        JsonElement input,
        ConformanceExecutionContext context,
        CancellationToken cancellationToken);
}

public sealed record ConformanceExecutionContext(
    string ScenarioId,
    int WorkBudget,
    int MaximumObservationBytes);

public sealed record ConformanceFixture(
    string SchemaVersion,
    string ScenarioId,
    Uri SourceDecision,
    string Capability,
    string ContractVersion,
    string InputDigest,
    JsonElement Input,
    JsonElement Expected,
    ImmutableArray<string> PermittedVariance,
    int WorkBudget,
    int MaximumObservationBytes)
{
    private static readonly ImmutableHashSet<string> AllowedProperties =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "schemaVersion",
            "scenarioId",
            "sourceDecision",
            "capability",
            "contractVersion",
            "inputDigest",
            "input",
            "expected",
            "permittedVariance",
            "workBudget",
            "maximumObservationBytes");

    public static ConformanceFixture Parse(string json)
    {
        try
        {
            return ParseCore(json);
        }
        catch (JsonException)
        {
            throw new ConformanceFixtureException(
                "fixture.malformed-json",
                "The conformance fixture is not valid JSON.");
        }
        catch (UriFormatException)
        {
            throw new ConformanceFixtureException(
                "fixture.invalid-property",
                "Conformance fixture property 'sourceDecision' must be an absolute URI.");
        }
    }

    private static ConformanceFixture ParseCore(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 64
        });
        var root = document.RootElement;
        ValidateProperties(root);

        var inputDigest = RequiredString(root, "inputDigest");
        if (!inputDigest.StartsWith("sha256:", StringComparison.Ordinal))
        {
            throw new ConformanceFixtureException(
                "fixture.invalid-property",
                "Conformance fixture property 'inputDigest' must start with 'sha256:'.");
        }

        var permittedVariance = ReadStrings(root, "permittedVariance");
        if (permittedVariance.Any(path => !path.StartsWith("/", StringComparison.Ordinal)) ||
            permittedVariance.Distinct(StringComparer.Ordinal).Count() != permittedVariance.Length)
        {
            throw new ConformanceFixtureException(
                "fixture.invalid-property",
                "Permitted variance must contain unique JSON pointers.");
        }
        if (permittedVariance.Any(path => !IsNonSemanticVariance(path)))
        {
            throw new ConformanceFixtureException(
                "fixture.semantic-variance-forbidden",
                "Permitted variance may target only operational data or rendered presentation text.");
        }

        var input = Required(root, "input").Clone();
        var computedDigest = $"sha256:{Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(ConformanceRunner.Normalize(input)))).ToLowerInvariant()}";
        if (!StringComparer.Ordinal.Equals(inputDigest, computedDigest))
        {
            throw new ConformanceFixtureException(
                "fixture.input-digest.mismatch",
                "The conformance fixture input digest does not match its normalized input.");
        }

        return new ConformanceFixture(
            RequiredString(root, "schemaVersion"),
            RequiredString(root, "scenarioId"),
            new Uri(RequiredString(root, "sourceDecision"), UriKind.Absolute),
            RequiredString(root, "capability"),
            RequiredString(root, "contractVersion"),
            inputDigest,
            input,
            Required(root, "expected").Clone(),
            permittedVariance,
            ReadPositiveInt(root, "workBudget", 100_000),
            ReadPositiveInt(root, "maximumObservationBytes", 1_048_576));
    }

    private static bool IsNonSemanticVariance(string path) =>
        path == "/operational" ||
        path.StartsWith("/operational/", StringComparison.Ordinal) ||
        path == "/presentation/renderedText" ||
        path.StartsWith("/presentation/renderedText/", StringComparison.Ordinal);

    private static void ValidateProperties(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ConformanceFixtureException(
                "fixture.invalid-property",
                "A conformance fixture must be a JSON object.");
        }

        var properties = root.EnumerateObject().Select(property => property.Name).ToArray();
        var duplicate = properties
            .GroupBy(property => property, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ConformanceFixtureException(
                "fixture.duplicate-property",
                $"Conformance fixture property '{duplicate.Key}' is duplicated.");
        }

        var unknown = properties.FirstOrDefault(property => !AllowedProperties.Contains(property));
        if (unknown is not null)
        {
            throw new ConformanceFixtureException(
                "fixture.unknown-property",
                $"Conformance fixture property '{unknown}' is not supported.");
        }
    }

    private static JsonElement Required(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value)
            ? value
            : throw new ConformanceFixtureException(
                "fixture.required-property",
                $"Conformance fixture property '{propertyName}' is required.");

    private static string RequiredString(JsonElement root, string propertyName)
    {
        var value = Required(root, propertyName);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ConformanceFixtureException(
                "fixture.invalid-property",
                $"Conformance fixture property '{propertyName}' must be a non-empty string.");
    }

    private static ImmutableArray<string> ReadStrings(JsonElement root, string propertyName) =>
        !root.TryGetProperty(propertyName, out var value)
            ? []
            : value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToImmutableArray()
                : throw new ConformanceFixtureException(
                    "fixture.invalid-property",
                    $"Conformance fixture property '{propertyName}' must be an array.");

    private static int ReadPositiveInt(JsonElement root, string propertyName, int defaultValue)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return defaultValue;
        }

        return value.TryGetInt32(out var result) && result > 0
            ? result
            : throw new ConformanceFixtureException(
                "fixture.invalid-property",
                $"Conformance fixture property '{propertyName}' must be a positive integer.");
    }
}

public sealed class ConformanceFixtureException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public enum ConformanceStatus
{
    Passed,
    Mismatch,
    Invalid,
    Cancelled,
    Failed
}

public sealed record ConformanceMismatch(
    string Path,
    string Code,
    string Message);

public sealed record ConformanceReport(
    string ScenarioId,
    ConformanceStatus Status,
    ImmutableArray<ConformanceMismatch> Mismatches);

public static class ConformanceRunner
{
    public static async ValueTask<ConformanceReport> RunAsync(
        ConformanceFixture fixture,
        IConformanceAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(adapter);

        if (!StringComparer.Ordinal.Equals(fixture.SchemaVersion, "1.0"))
        {
            return Invalid(fixture, "fixture.schema.unsupported", "The fixture schema version is not supported.");
        }

        if (!StringComparer.Ordinal.Equals(fixture.Capability, adapter.Capability))
        {
            return Invalid(fixture, "adapter.capability.mismatch", "The adapter does not implement the fixture capability.");
        }

        if (!StringComparer.Ordinal.Equals(fixture.ContractVersion, adapter.ContractVersion))
        {
            return Invalid(fixture, "adapter.contract-version.mismatch", "The adapter contract version does not match the fixture.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observation = await adapter.ExecuteAsync(
                fixture.Input,
                new ConformanceExecutionContext(
                    fixture.ScenarioId,
                    fixture.WorkBudget,
                    fixture.MaximumObservationBytes),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var actual = Normalize(observation);
            if (Encoding.UTF8.GetByteCount(actual) > fixture.MaximumObservationBytes)
            {
                return Invalid(fixture, "observation.limit.exceeded", "The observation exceeded the declared byte limit.");
            }

            var mismatches = Compare(
                fixture.Expected,
                observation,
                fixture.PermittedVariance.ToImmutableHashSet(StringComparer.Ordinal),
                string.Empty);
            return mismatches.IsEmpty
                ? new ConformanceReport(fixture.ScenarioId, ConformanceStatus.Passed, [])
                : new ConformanceReport(
                    fixture.ScenarioId,
                    ConformanceStatus.Mismatch,
                    mismatches);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ConformanceReport(fixture.ScenarioId, ConformanceStatus.Cancelled, []);
        }
        catch (Exception)
        {
            return new ConformanceReport(
                fixture.ScenarioId,
                ConformanceStatus.Failed,
                [new ConformanceMismatch("/", "adapter.failed", "The conformance adapter failed.")]);
        }
    }

    private static ConformanceReport Invalid(
        ConformanceFixture fixture,
        string code,
        string message) =>
        new(
            fixture.ScenarioId,
            ConformanceStatus.Invalid,
            [new ConformanceMismatch("/", code, message)]);

    internal static string Normalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteNormalized(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static ImmutableArray<ConformanceMismatch> Compare(
        JsonElement expected,
        JsonElement actual,
        ImmutableHashSet<string> permittedVariance,
        string path)
    {
        var pointer = path.Length == 0 ? "/" : path;
        if (permittedVariance.Contains(pointer) ||
            permittedVariance.Any(permitted =>
                permitted != "/" && pointer.StartsWith($"{permitted}/", StringComparison.Ordinal)))
        {
            return [];
        }

        if (expected.ValueKind != actual.ValueKind)
        {
            return [Mismatch(pointer, "observation.kind-mismatch", "The JSON value kind did not match.")];
        }

        if (expected.ValueKind == JsonValueKind.Object)
        {
            var expectedProperties = expected.EnumerateObject().ToDictionary(property => property.Name, StringComparer.Ordinal);
            var actualProperties = actual.EnumerateObject().ToDictionary(property => property.Name, StringComparer.Ordinal);
            var names = expectedProperties.Keys
                .Concat(actualProperties.Keys)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);
            var builder = ImmutableArray.CreateBuilder<ConformanceMismatch>();
            foreach (var name in names)
            {
                var childPath = $"{path}/{Escape(name)}";
                if (!expectedProperties.TryGetValue(name, out var expectedProperty))
                {
                    builder.Add(Mismatch(childPath, "observation.unexpected", "The observation contained an unexpected property."));
                }
                else if (!actualProperties.TryGetValue(name, out var actualProperty))
                {
                    builder.Add(Mismatch(childPath, "observation.missing", "The observation omitted an expected property."));
                }
                else
                {
                    builder.AddRange(Compare(expectedProperty.Value, actualProperty.Value, permittedVariance, childPath));
                }
            }

            return builder.ToImmutable();
        }

        if (expected.ValueKind == JsonValueKind.Array)
        {
            var expectedItems = expected.EnumerateArray().ToArray();
            var actualItems = actual.EnumerateArray().ToArray();
            var builder = ImmutableArray.CreateBuilder<ConformanceMismatch>();
            var length = Math.Max(expectedItems.Length, actualItems.Length);
            for (var index = 0; index < length; index++)
            {
                var childPath = $"{path}/{index}";
                if (index >= expectedItems.Length)
                {
                    builder.Add(Mismatch(childPath, "observation.unexpected", "The observation contained an unexpected array item."));
                }
                else if (index >= actualItems.Length)
                {
                    builder.Add(Mismatch(childPath, "observation.missing", "The observation omitted an expected array item."));
                }
                else
                {
                    builder.AddRange(Compare(expectedItems[index], actualItems[index], permittedVariance, childPath));
                }
            }

            return builder.ToImmutable();
        }

        return StringComparer.Ordinal.Equals(Normalize(expected), Normalize(actual))
            ? []
            : [Mismatch(pointer, "observation.value-mismatch", "The semantic value did not match.")];
    }

    private static ConformanceMismatch Mismatch(string path, string code, string message) =>
        new(path, code, message);

    private static string Escape(string propertyName) =>
        propertyName.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static void WriteNormalized(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteNormalized(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteNormalized(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
