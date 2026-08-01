using System.Collections.Immutable;

namespace Modeller.Model;

public sealed record AuthoredContextRevision
{
    private AuthoredContextRevision(
        SemanticId id,
        SemanticName name,
        SemanticSlug slug,
        string contextVersion,
        ImmutableArray<SemanticDefinition> definitions,
        long revision)
    {
        Id = id;
        Name = name;
        Slug = slug;
        ContextVersion = contextVersion;
        Definitions = definitions;
        Revision = revision;
    }

    public SemanticId Id { get; }
    public SemanticName Name { get; }
    public SemanticSlug Slug { get; }
    public string ContextVersion { get; }
    public ImmutableArray<SemanticDefinition> Definitions { get; }
    public long Revision { get; }

    public static AuthoredContextRevision Create(
        SemanticId id,
        SemanticName name,
        SemanticSlug slug,
        string contextVersion) =>
        new(
            id,
            name,
            slug,
            string.IsNullOrWhiteSpace(contextVersion)
                ? throw new ArgumentException("A context version is required.", nameof(contextVersion))
                : contextVersion.Trim(),
            [],
            0);

    public SemanticConceptAddress? FindConcept(SemanticId id) =>
        SemanticNavigation.Concepts(this).FirstOrDefault(concept => concept.Id == id);

    internal AuthoredContextRevision Next(ImmutableArray<SemanticDefinition> definitions) =>
        new(Id, Name, Slug, ContextVersion, definitions, checked(Revision + 1));
}
