namespace Modeller.Workspace;

/// <summary>
/// A document or resource path confined to a logical workspace boundary — never rooted, never
/// escaping via ".." segments, never containing a NUL byte, never blank. Construction is the only
/// way to obtain one, so a validated <see cref="LogicalPath"/> cannot become invalid later.
/// </summary>
public readonly record struct LogicalPath
{
    private LogicalPath(string value) => Value = value;

    /// <summary>The confined, forward-slash-normalized path.</summary>
    public string Value { get; }

    public static bool TryCreate(string? candidate, out LogicalPath path)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('\0') || IsRooted(candidate))
        {
            path = default;
            return false;
        }

        var normalized = candidate.Replace('\\', '/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
        {
            path = default;
            return false;
        }

        path = new LogicalPath(normalized);
        return true;
    }

    public static LogicalPath Create(string candidate) =>
        TryCreate(candidate, out var path) ? path : throw new ArgumentException($"'{candidate}' is not a confined logical path.", nameof(candidate));

    private static bool IsRooted(string value) =>
        Path.IsPathRooted(value) || value.StartsWith('/') || value.StartsWith('\\') ||
        (value.Length >= 2 && value[1] == ':' && char.IsAsciiLetter(value[0]));

    public override string ToString() => Value;
    public static implicit operator string(LogicalPath path) => path.Value;
}
