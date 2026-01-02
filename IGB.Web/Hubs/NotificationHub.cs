using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace IGB.Web.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public static string UserGroup(string userId) => $"user:{userId}";
    public static string RoleGroup(string role) => $"role:{role.ToLowerInvariant()}";

    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
            Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        var roles = Context.User?.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList() ?? new List<string>();
        foreach (var role in roles)
            Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role));

        return base.OnConnectedAsync();
    }
}


