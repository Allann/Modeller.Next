using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Modeller.Configuration;

public enum ConfigurationSourceKind { Base, Profile, Override }
public sealed record ConfigurationValue(string Value, bool IsSecret = false);
public sealed record ConfigurationSource(string Id, string Version, ConfigurationSourceKind Kind, string? Profile,
    ImmutableDictionary<string, ConfigurationValue> Values);
public sealed record ConfigurationRequest(ImmutableArray<ConfigurationSource> Sources, string? Profile);
public sealed record RuntimeConfiguration(string GenerationContractVersion, string LogicalOutputRoot,
    ImmutableDictionary<string, string> Variables);
public sealed record ConfigurationProvenance(string SourceId, string SourceVersion, ConfigurationSourceKind Kind, string DisplayValue);
public sealed record ConfigurationDiagnostic(string Code, string Message, string? Field = null);
public sealed record ConfigurationResult(RuntimeConfiguration? Configuration,
    ImmutableDictionary<string, ConfigurationProvenance> Provenance, ImmutableArray<ConfigurationDiagnostic> Diagnostics)
{ public bool IsSuccess => Configuration is not null && Diagnostics.IsEmpty; }

public static partial class ConfigurationResolver
{
    public static ConfigurationResult Resolve(ConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested) return Failure("configuration.cancelled", "Configuration resolution was cancelled.");
        var values = new Dictionary<string, ConfigurationValue>(StringComparer.Ordinal);
        var provenance = new Dictionary<string, ConfigurationProvenance>(StringComparer.Ordinal);
        foreach (var source in request.Sources.OrderBy(s => s.Kind))
        {
            if (source.Version != "1.0") return Failure("configuration.version.unsupported", "A configuration source version is unsupported.");
            if (source.Kind == ConfigurationSourceKind.Profile && source.Profile != request.Profile) continue;
            foreach (var entry in source.Values.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                values[entry.Key] = entry.Value;
                provenance[entry.Key] = new(source.Id, source.Version, source.Kind, entry.Value.IsSecret ? "[secret]" : entry.Value.Value);
            }
        }
        var diagnostics = ImmutableArray.CreateBuilder<ConfigurationDiagnostic>();
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in values)
        {
            var value = Substitution().Replace(entry.Value.Value, match =>
            {
                var key = match.Groups["key"].Value;
                if (values.TryGetValue(key, out var replacement)) return replacement.Value;
                diagnostics.Add(new("configuration.variable.unresolved", $"Configuration field '{entry.Key}' references an unavailable value.", entry.Key));
                return string.Empty;
            });
            resolved[entry.Key] = value;
        }
        if (!resolved.TryGetValue("generationContractVersion", out var contract)) diagnostics.Add(new("configuration.field.required", "Generation contract version is required.", "generationContractVersion"));
        if (!resolved.TryGetValue("logicalOutputRoot", out var root)) diagnostics.Add(new("configuration.field.required", "Logical output root is required.", "logicalOutputRoot"));
        if (diagnostics.Count > 0) return new(null, provenance.ToImmutableDictionary(), diagnostics.ToImmutable());
        var variables = resolved.Where(x => x.Key.StartsWith("variables.", StringComparison.Ordinal))
            .ToImmutableDictionary(x => x.Key[10..], x => x.Value, StringComparer.Ordinal);
        return new(new(contract!, root!, variables), provenance.ToImmutableDictionary(), []);
    }

    private static ConfigurationResult Failure(string code, string message) => new(null, ImmutableDictionary<string, ConfigurationProvenance>.Empty, [new(code, message)]);
    [GeneratedRegex("\\$\\{(?<key>[A-Za-z0-9_.-]+)\\}", RegexOptions.CultureInvariant)] private static partial Regex Substitution();
}
