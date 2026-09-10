using Microsoft.AspNetCore.SignalR;

namespace NotificationService.API.Hubs;

/// <summary>
/// Real-time push endpoint for reactive communication. Clients connect and join their own
/// recipient group; NotificationBroadcastService pushes batched notifications into that
/// group as they flow through the Rx.NET stream.
/// </summary>
public class NotificationHub : Hub
{
    public async Task JoinRecipientGroup(int recipientId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(recipientId));
    }

    public async Task LeaveRecipientGroup(int recipientId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(recipientId));
    }

    public static string GroupName(int recipientId) => $"recipient-{recipientId}";
}
