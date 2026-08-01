namespace Modeller.Model;

public readonly record struct SemanticId
{
    private SemanticId(Guid value) => Value = value;

    public Guid Value { get; }

    public static SemanticId New() => new(Guid.CreateVersion7());

    public static SemanticId Parse(string value)
    {
        var parsed = Guid.Parse(value);
        return parsed.Version == 7
            ? new SemanticId(parsed)
            : throw new ArgumentException("Semantic identities must be UUIDv7 values.", nameof(value));
    }

    public override string ToString() => Value.ToString("D");
}

public sealed record SemanticName
{
    public SemanticName(string value) =>
        Value = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A semantic name is required.", nameof(value))
            : value.Trim();

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record SemanticSlug
{
    public SemanticSlug(string value)
    {
        var candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length == 0 || candidate.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException(
                "A semantic slug must contain only ASCII letters, digits, and hyphens.",
                nameof(value));
        }

        Value = candidate;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
