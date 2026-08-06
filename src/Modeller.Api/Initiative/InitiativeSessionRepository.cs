using System.Text.Json;
using Modeller.Initiative;

namespace Modeller.Api.Initiative;

public interface IInitiativeSessionRepository
{
    Task SaveAsync(InitiativeSession session, CancellationToken cancellationToken = default);

    Task<InitiativeSession?> LoadAsync(InitiativeId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// One JSON file per session (<c>{root}/{id}.json</c>), matching Business Statement's v1 persistence
/// decision (M:\business-statement\src\BusinessStatement.Adapters.Files\JsonFileSessionRepository.cs)
/// — sessions are expected to be low volume, so file-per-session is simple and sufficient; a database
/// adapter is a natural later swap behind the same <see cref="IInitiativeSessionRepository"/> port.
/// </summary>
public sealed class JsonFileInitiativeSessionRepository(string rootDirectory) : IInitiativeSessionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task SaveAsync(InitiativeSession session, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(rootDirectory);
        var dto = InitiativeSessionMapper.ToDto(session);
        await using var stream = File.Create(PathFor(session.Id));
        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken);
    }

    public async Task<InitiativeSession?> LoadAsync(InitiativeId id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (!File.Exists(path)) return null;

        await using var stream = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<InitiativeSessionDto>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"The Initiative session document at '{path}' was empty.");
        return InitiativeSessionMapper.ToDomain(dto);
    }

    private string PathFor(InitiativeId id) => Path.Combine(rootDirectory, $"{id.Value:D}.json");
}
