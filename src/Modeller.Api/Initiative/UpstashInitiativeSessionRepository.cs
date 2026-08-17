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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(InitiativeSession session, CancellationToken cancellationToken = default)
    {
        var document = JsonSerializer.Serialize(InitiativeSessionMapper.ToDto(session), JsonOptions);
        var result = await ExecuteAsync(["SET", KeyFor(session.Id), document], cancellationToken);
        if (!string.Equals(result, "OK", StringComparison.Ordinal))
            throw new InvalidOperationException("Upstash Redis did not confirm the Initiative session save.");
    }

    public async Task<InitiativeSession?> LoadAsync(InitiativeId id, CancellationToken cancellationToken = default)
    {
        var document = await ExecuteAsync(["GET", KeyFor(id)], cancellationToken);
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

    private static string KeyFor(InitiativeId id) => $"{KeyPrefix}{id.Value:D}";

    private sealed record UpstashResponse(string? Result, string? Error);
}
