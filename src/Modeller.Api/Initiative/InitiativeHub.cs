using Microsoft.AspNetCore.SignalR;

namespace Modeller.Api.Initiative;

/// <summary>
/// Realtime session updates (issue #90 / ADR 0001 in M:\business-statement: "Use SignalR for
/// realtime session updates"). Deliberately thin: clients join a per-session group and receive a
/// bare "the session changed, go refetch" notification rather than a partial delta — v1 dropped
/// Business Statement's role-scoped visibility filtering (issue #88), so there's no per-role payload
/// to compute, and refetching <c>GET /v1/initiative/{id}</c> is simple and always consistent.
/// </summary>
public sealed class InitiativeHub : Hub
{
    public const string SessionUpdated = "InitiativeSessionUpdated";

    public Task JoinSession(Guid initiativeId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(initiativeId));

    public Task LeaveSession(Guid initiativeId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(initiativeId));

    public static string GroupName(Guid initiativeId) => $"initiative:{initiativeId:D}";
}
