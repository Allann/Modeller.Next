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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var command = JsonSerializer.Deserialize<string[]>(
                await request.Content!.ReadAsStringAsync(cancellationToken))!;
            string response;

            if (error is not null)
            {
                response = JsonSerializer.Serialize(new { error });
            }
            else if (command[0] == "SET")
            {
                _values[command[1]] = command[2];
                response = "{\"result\":\"OK\"}";
            }
            else
            {
                _values.TryGetValue(command[1], out var value);
                response = JsonSerializer.Serialize(new { result = value });
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
