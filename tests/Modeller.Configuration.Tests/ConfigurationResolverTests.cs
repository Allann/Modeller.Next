using System.Collections.Immutable;
using Modeller.Configuration;
using Xunit;

namespace Modeller.Configuration.Tests;

public sealed class ConfigurationResolverTests
{
    [Fact]
    public void Child_care_profile_and_override_resolve_with_field_provenance()
    {
        var result = ConfigurationResolver.Resolve(new ConfigurationRequest([
            Source("base", ConfigurationSourceKind.Base, null, ("generationContractVersion", "1.0"), ("logicalOutputRoot", "generated"), ("variables.namespace", "ChildCare")),
            Source("development", ConfigurationSourceKind.Profile, "development", ("variables.namespace", "ChildCare.Development")),
            Source("command", ConfigurationSourceKind.Override, null, ("logicalOutputRoot", "preview"))
        ], "development"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("preview", result.Configuration!.LogicalOutputRoot);
        Assert.Equal("ChildCare.Development", result.Configuration.Variables["namespace"]);
        Assert.Equal("command", result.Provenance["logicalOutputRoot"].SourceId);
        Assert.Equal("development", result.Provenance["variables.namespace"].SourceId);
    }

    [Fact]
    public void Missing_substitution_and_secrets_return_redacted_diagnostics_and_provenance()
    {
        var result = ConfigurationResolver.Resolve(new ConfigurationRequest([
            Source("base", ConfigurationSourceKind.Base, null,
                ("generationContractVersion", "1.0"), ("logicalOutputRoot", "${missing}")),
            new ConfigurationSource("secrets", "1.0", ConfigurationSourceKind.Override, null,
                ImmutableDictionary<string, ConfigurationValue>.Empty.Add("variables.token", new("super-secret", true)))
        ], null), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("super-secret", string.Join(' ', result.Diagnostics.Select(d => d.Message)), StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", string.Join(' ', result.Provenance.Values.Select(p => p.DisplayValue)), StringComparison.Ordinal);
        Assert.Equal("configuration.variable.unresolved", Assert.Single(result.Diagnostics).Code);
    }

    private static ConfigurationSource Source(string id, ConfigurationSourceKind kind, string? profile, params (string Key, string Value)[] entries) =>
        new(id, "1.0", kind, profile, entries.ToImmutableDictionary(x => x.Key, x => new ConfigurationValue(x.Value), StringComparer.Ordinal));
}
