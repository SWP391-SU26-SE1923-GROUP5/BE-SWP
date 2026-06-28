using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AIStudyHub.API.Hubs;

/// <summary>
/// SignalR hub for real-time notifications.
/// Lives in the API layer because it is a network transport endpoint, not a business rule.
/// Clients connect to <c>/hubs/notifications</c> and join their own userId group to receive targeted messages.
/// The Business layer's RealTimeNotificationService broadcasts to these groups using IHubContext&lt;Hub&gt;
/// without needing to reference this concrete class.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public async Task JoinGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }
    }

    public async Task LeaveGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        }
    }

    public override Task OnConnectedAsync() => base.OnConnectedAsync();

    public override Task OnDisconnectedAsync(Exception? exception) => base.OnDisconnectedAsync(exception);
}