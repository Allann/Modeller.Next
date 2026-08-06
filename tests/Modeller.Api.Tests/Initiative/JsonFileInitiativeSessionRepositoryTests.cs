using Modeller.Api.Initiative;
using Modeller.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

public sealed class JsonFileInitiativeSessionRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "modeller-initiative-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsTheSession()
    {
        var repository = new JsonFileInitiativeSessionRepository(_root);
        var session = InitiativeSession.CreateNew("Build us a new approval system");
        session = session.AddParticipant(Participant.CreateNew("Alex", ParticipantRole.Facilitator));

        await repository.SaveAsync(session, TestContext.Current.CancellationToken);
        var loaded = await repository.LoadAsync(session.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(session.Id, loaded.Id);
        Assert.Equal(session.OriginalChangeRequest, loaded.OriginalChangeRequest);
        // InitiativeSession.CreateNew auto-adds the Agent participant, so this is Agent + Facilitator.
        Assert.Equal(2, loaded.Participants.Count);
        Assert.Contains(loaded.Participants, p => p.Role == ParticipantRole.Facilitator && p.DisplayName == "Alex");
    }

    [Fact]
    public async Task LoadAsync_UnknownId_ReturnsNull()
    {
        var repository = new JsonFileInitiativeSessionRepository(_root);

        var loaded = await repository.LoadAsync(InitiativeId.New(), TestContext.Current.CancellationToken);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveAsync_OverwritesAPreviouslySavedSession()
    {
        var repository = new JsonFileInitiativeSessionRepository(_root);
        var session = InitiativeSession.CreateNew("Build us a new approval system");
        await repository.SaveAsync(session, TestContext.Current.CancellationToken);

        session = session.AddParticipant(Participant.CreateNew("Alex", ParticipantRole.Facilitator));
        await repository.SaveAsync(session, TestContext.Current.CancellationToken);

        var loaded = await repository.LoadAsync(session.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, loaded!.Participants.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
