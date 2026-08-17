using System.Net.Http.Json;
using System.Text.Json;
using Modeller.Initiative;

namespace Modeller.Api.Initiative;

/// <summary>
/// Stores Initiative sessions in Upstash Redis through its serverless REST API.
/// </summary>
public sealed class UpstashInitiativeSessionRepository(HttpClient client) : IInitiativeSessionRepository
{
    private const string KeyPrefix = "modeller:initiative:";
    private const string ArchiveKeyPrefix = "modeller:initiative:archive:";
    private const int RetentionSeconds = 7 * 24 * 60 * 60;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(InitiativeSession session, CancellationToken cancellationToken = default)
    {
        var document = JsonSerializer.Serialize(InitiativeSessionMapper.ToDto(session), JsonOptions);
        var savedKey = session.Finalization is null ? ActiveKeyFor(session.Id) : ArchiveKeyFor(session.Id);
        var removedKey = session.Finalization is null ? ArchiveKeyFor(session.Id) : ActiveKeyFor(session.Id);
        var results = await ExecuteTransactionAsync(
            [
                ["SET", savedKey, document, "EX", RetentionSeconds.ToString()],
                ["DEL", removedKey],
            ],
            cancellationToken);

        if (results.Count != 2
            || results[0].Result.ValueKind != JsonValueKind.String
            || !string.Equals(results[0].Result.GetString(), "OK", StringComparison.Ordinal))
            throw new InvalidOperationException("Upstash Redis did not confirm the Initiative session save.");
    }

    public async Task<InitiativeSession?> LoadAsync(InitiativeId id, CancellationToken cancellationToken = default)
    {
        var document = await ExecuteAsync(["GET", ActiveKeyFor(id)], cancellationToken)
            ?? await ExecuteAsync(["GET", ArchiveKeyFor(id)], cancellationToken);
        if (document is null) return null;

        var dto = JsonSerializer.Deserialize<InitiativeSessionDto>(document, JsonOptions)
            ?? throw new InvalidOperationException($"The Initiative session document for '{id.Value:D}' was empty.");
        return InitiativeSessionMapper.ToDomain(dto);
    }

    private async Task<string?> ExecuteAsync(string[] command, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("", command, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<UpstashResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Upstash Redis returned an empty response.");
        if (!string.IsNullOrWhiteSpace(body.Error))
            throw new InvalidOperationException($"Upstash Redis rejected the Initiative session command: {body.Error}");
        return body.Result;
    }

    private async Task<IReadOnlyList<UpstashTransactionResponse>> ExecuteTransactionAsync(
        string[][] commands,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("multi-exec", commands, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<List<UpstashTransactionResponse>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Upstash Redis returned an empty transaction response.");
        var error = body.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Error))?.Error;
        if (error is not null)
            throw new InvalidOperationException($"Upstash Redis rejected the Initiative session transaction: {error}");
        return body;
    }

    private static string ActiveKeyFor(InitiativeId id) => $"{KeyPrefix}{id.Value:D}";

    private static string ArchiveKeyFor(InitiativeId id) => $"{ArchiveKeyPrefix}{id.Value:D}";

    private sealed record UpstashResponse(string? Result, string? Error);

    private sealed record UpstashTransactionResponse(JsonElement Result, string? Error);
}
