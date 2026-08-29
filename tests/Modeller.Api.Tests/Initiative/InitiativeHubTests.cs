using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Modeller.Api.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

/// <summary>
/// Issue #90's own verification requirement: "a second connected client receives a SignalR update
/// after the first client's command completes." One client submits a command over plain HTTP; a
/// second, independent SignalR connection (simulating the other role's browser tab) must observe
/// the notification — proving the realtime path actually works end to end, not just that the hub
/// is mapped.
/// </summary>
public sealed class InitiativeHubTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "modeller-initiative-hub-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;

    public InitiativeHubTests() =>
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("Initiative:StorageRoot", _storageRoot));

    [Fact]
    public async Task ASecondConnectedClient_ReceivesANotification_AfterAnotherClientsCommandCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var httpClient = _factory.CreateClient();
        var created = await httpClient.PostAsJsonAsync(
            "/v1/initiative", new CreateInitiativeRequest("Build us a new approval system", "Alex", "Jordan"), ApiJson.Options, ct);
        var createdBody = await created.Content.ReadFromJsonAsync<CreateInitiativeResponseDto>(ApiJson.Options, ct);
        var session = createdBody!.Session;

        await using var connection = new HubConnectionBuilder()
            .WithUrl($"{httpClient.BaseAddress}hubs/initiative", options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        var notificationReceived = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<Guid>(InitiativeHub.SessionUpdated, id => notificationReceived.TrySetResult(id));

        await connection.StartAsync(ct);
        await connection.InvokeAsync("JoinSession", session.Id, ct);

        using var proposeRequest = new HttpRequestMessage(HttpMethod.Post, $"/v1/initiative/{session.Id}/questions")
        {
            Content = JsonContent.Create(new ProposeQuestionRequestDto("PainPoints", "What's painful today?"), options: ApiJson.Options),
        };
        proposeRequest.Headers.Add("X-Initiative-Credential", createdBody.Credentials.Facilitator);
        var propose = await httpClient.SendAsync(proposeRequest, ct);
        propose.EnsureSuccessStatusCode();

        var receivedId = await notificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.Equal(session.Id, receivedId);
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
    }
}
