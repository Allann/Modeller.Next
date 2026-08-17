using System.Net;
using System.Text;
using System.Text.Json;
using Modeller.Api.Initiative;
using Modeller.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

public sealed class UpstashInitiativeSessionRepositoryTests
{
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsTheSessionAcrossRepositoryInstances()
    {
        var redis = new FakeUpstashHandler();
        var session = InitiativeSession.CreateNew("Build us a new approval system")
            .AddParticipant(Participant.CreateNew("Alex", ParticipantRole.Facilitator));

        await CreateRepository(redis).SaveAsync(session, TestContext.Current.CancellationToken);
        var loaded = await CreateRepository(redis).LoadAsync(session.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(session.Id, loaded.Id);
        Assert.Equal(session.OriginalChangeRequest, loaded.OriginalChangeRequest);
        Assert.Contains(loaded.Participants, participant =>
            participant.Role == ParticipantRole.Facilitator && participant.DisplayName == "Alex");
        Assert.All(redis.SetCommands, command => Assert.Equal(["EX", "604800"], command[3..]));
    }

    [Fact]
    public async Task SaveAsync_FinalizedSession_MovesItToTheSevenDayArchive()
    {
        var redis = new FakeUpstashHandler();
        var session = InitiativeSession.CreateNew("Build us a new approval system");
        var repository = CreateRepository(redis);

        await repository.SaveAsync(session, TestContext.Current.CancellationToken);
        session = session.FinalizeInitiative(DateTimeOffset.Parse("2026-08-17T00:00:00Z"), null);
        await repository.SaveAsync(session, TestContext.Current.CancellationToken);

        Assert.DoesNotContain($"modeller:initiative:{session.Id.Value:D}", redis.Keys);
        Assert.Contains($"modeller:initiative:archive:{session.Id.Value:D}", redis.Keys);
        Assert.NotNull((await repository.LoadAsync(session.Id, TestContext.Current.CancellationToken))?.Finalization);
        Assert.All(redis.SetCommands, command => Assert.Equal(["EX", "604800"], command[3..]));
    }

    [Fact]
    public async Task SaveAsync_ReopenedSession_MovesItBackToTheActiveStore()
    {
        var redis = new FakeUpstashHandler();
        var repository = CreateRepository(redis);
        var session = InitiativeSession.CreateNew("Build us a new approval system")
            .FinalizeInitiative(DateTimeOffset.Parse("2026-08-17T00:00:00Z"), null);
        await repository.SaveAsync(session, TestContext.Current.CancellationToken);

        await repository.SaveAsync(session.Reopen(), TestContext.Current.CancellationToken);

        Assert.Contains($"modeller:initiative:{session.Id.Value:D}", redis.Keys);
        Assert.DoesNotContain($"modeller:initiative:archive:{session.Id.Value:D}", redis.Keys);
        Assert.Null((await repository.LoadAsync(session.Id, TestContext.Current.CancellationToken))?.Finalization);
    }

    [Fact]
    public async Task LoadAsync_UnknownId_ReturnsNull()
    {
        var loaded = await CreateRepository(new FakeUpstashHandler())
            .LoadAsync(InitiativeId.New(), TestContext.Current.CancellationToken);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveAsync_UpstashError_Throws()
    {
        var repository = CreateRepository(new FakeUpstashHandler("ERR storage unavailable"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(
            InitiativeSession.CreateNew("Build us a new approval system"),
            TestContext.Current.CancellationToken));

        Assert.Contains("ERR storage unavailable", exception.Message, StringComparison.Ordinal);
    }

    private static UpstashInitiativeSessionRepository CreateRepository(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.upstash.io/") });

    private sealed class FakeUpstashHandler(string? error = null) : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _values = [];

        public IEnumerable<string> Keys => _values.Keys;

        public List<string[]> SetCommands { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = await request.Content!.ReadAsStringAsync(cancellationToken);
            string response;

            if (error is not null)
            {
                response = request.RequestUri!.AbsolutePath.EndsWith("multi-exec", StringComparison.Ordinal)
                    ? JsonSerializer.Serialize(new[] { new { error } })
                    : JsonSerializer.Serialize(new { error });
            }
            else if (request.RequestUri!.AbsolutePath.EndsWith("multi-exec", StringComparison.Ordinal))
            {
                var commands = JsonSerializer.Deserialize<string[][]>(content)!;
                response = JsonSerializer.Serialize(commands.Select(Execute));
            }
            else
            {
                response = JsonSerializer.Serialize(Execute(JsonSerializer.Deserialize<string[]>(content)!));
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }

        private object Execute(string[] command)
        {
            if (command[0] == "SET")
            {
                SetCommands.Add(command);
                _values[command[1]] = command[2];
                return new { result = (object?)"OK" };
            }

            if (command[0] == "DEL")
                return new { result = (object?)(_values.Remove(command[1]) ? 1 : 0) };

            _values.TryGetValue(command[1], out var value);
            return new { result = (object?)value };
        }
    }
}
