using DaycareAPI.Data;
using DaycareAPI.Models;
using DaycareAPI.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DaycareAPI.Services
{
    public interface INotificationService
    {
        Task SendEventNotificationToParentsAsync(Event eventItem);
        Task SendNotificationAsync(string userId, string title, string message, string type = "General", string? redirectUrl = null);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hubContext,
            IPushNotificationService pushNotificationService,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
        }

        public async Task SendEventNotificationToParentsAsync(Event eventItem)
        {
            try
            {
                // Get all users with Parent role
                var parentUsers = await _context.Users
                    .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { User = u, ur.RoleId })
                    .Join(_context.Roles, x => x.RoleId, r => r.Id, (x, r) => new { x.User, Role = r })
                    .Where(x => x.Role.Name == "Parent")
                    .Select(x => x.User)
                    .ToListAsync();

                Console.WriteLine($"Found {parentUsers.Count} parent users for event notification");
                
                foreach (var user in parentUsers)
                {
                    Console.WriteLine($"Sending notification to parent: {user.Email}");
                    await SendNotificationAsync(
                        user.Id,
                        "New Event Created",
                        $"A new event '{eventItem.Name}' has been scheduled. Check it out!",
                        "Event",
                        $"/events/detail/{eventItem.Id}"
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending event notifications: {ex.Message}");
            }
        }

        public async Task SendNotificationAsync(string userId, string title, string message, string type = "General", string? redirectUrl = null)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Type = type,
                    RedirectUrl = redirectUrl,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Notification saved to DB for user {userId}: {title}");

                // Send real-time notification via SignalR (for when app is open)
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    userId = notification.UserId,
                    title = notification.Title,
                    message = notification.Message,
                    type = notification.Type,
                    redirectUrl = notification.RedirectUrl,
                    isRead = notification.IsRead,
                    createdAt = notification.CreatedAt
                });

                _logger.LogInformation($"SignalR notification sent to User_{userId}");

                // Send FCM push notification (for when app is closed/backgrounded)
                var pushData = new Dictionary<string, string>
                {
                    { "notificationId", notification.Id.ToString() },
                    { "type", type },
                    { "redirectUrl", redirectUrl ?? "" }
                };

                var pushSent = await _pushNotificationService.SendPushNotificationAsync(userId, title, message, pushData);

                if (pushSent)
                {
                    _logger.LogInformation($"FCM push notification sent to user {userId}");
                }
                else
                {
                    _logger.LogInformation($"FCM push notification not sent to user {userId} (no active device tokens or Firebase not configured)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to user {userId}");
            }
        }
    }
}