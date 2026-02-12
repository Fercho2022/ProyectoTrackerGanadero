using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace ApiWebTrackerGanado.Hubs
{
    [Authorize]
    public class LiveTrackingHub : Hub
    {
        public async Task JoinFarmGroup(string farmId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"farm_{farmId}");
        }

        public async Task LeaveFarmGroup(string farmId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"farm_{farmId}");
        }

        public async Task JoinAnimalGroup(string animalId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"animal_{animalId}");
        }

        public async Task LeaveAnimalGroup(string animalId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"animal_{animalId}");
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst("sub")?.Value;
            if (userId != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst("sub")?.Value;
            if (userId != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
