using DaycareAPI.Data;
using DaycareAPI.Models;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;

namespace DaycareAPI.Services
{
    public interface IPushNotificationService
    {
        Task<bool> SendPushNotificationAsync(string userId, string title, string body, Dictionary<string, string>? data = null);
        Task<bool> SendPushNotificationToTokenAsync(string token, string title, string body, Dictionary<string, string>? data = null);
        Task<int> SendPushNotificationToMultipleAsync(List<string> userIds, string title, string body, Dictionary<string, string>? data = null);
        Task<DeviceToken?> RegisterDeviceTokenAsync(string userId, string token, string platform, string? deviceModel = null);
        Task<bool> UnregisterDeviceTokenAsync(string userId, string token);
        Task<bool> DeactivateTokenAsync(string token);
    }

    public class PushNotificationService : IPushNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PushNotificationService> _logger;
        private static bool _firebaseInitialized = false;
        private static readonly object _lock = new object();

        public PushNotificationService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<PushNotificationService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            InitializeFirebase();
        }

        private void InitializeFirebase()
        {
            lock (_lock)
            {
                if (_firebaseInitialized) return;

                try
                {
                    var credentialsPath = _configuration["Firebase:CredentialsPath"];

                    if (string.IsNullOrEmpty(credentialsPath))
                    {
                        _logger.LogWarning("Firebase credentials path not configured. Push notifications will not work.");
                        return;
                    }

                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), credentialsPath);

                    if (!File.Exists(fullPath))
                    {
                        _logger.LogWarning($"Firebase credentials file not found at: {fullPath}. Push notifications will not work.");
                        return;
                    }

                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(fullPath),
                        ProjectId = _configuration["Firebase:ProjectId"]
                    });

                    _firebaseInitialized = true;
                    _logger.LogInformation("Firebase initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Firebase");
                }
            }
        }

        public async Task<DeviceToken?> RegisterDeviceTokenAsync(string userId, string token, string platform, string? deviceModel = null)
        {
            try
            {
                // Check if token already exists
                var existingToken = await _context.DeviceTokens
                    .FirstOrDefaultAsync(dt => dt.Token == token);

                if (existingToken != null)
                {
                    // Update existing token
                    existingToken.UserId = userId;
                    existingToken.Platform = platform;
                    existingToken.DeviceModel = deviceModel;
                    existingToken.IsActive = true;
                    existingToken.UpdatedAt = DateTime.UtcNow;
                    existingToken.LastUsedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new token
                    existingToken = new DeviceToken
                    {
                        UserId = userId,
                        Token = token,
                        Platform = platform,
                        DeviceModel = deviceModel,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastUsedAt = DateTime.UtcNow
                    };
                    _context.DeviceTokens.Add(existingToken);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Device token registered for user {userId} on {platform}");
                return existingToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to register device token for user {userId}");
                return null;
            }
        }

        public async Task<bool> UnregisterDeviceTokenAsync(string userId, string token)
        {
            try
            {
                var deviceToken = await _context.DeviceTokens
                    .FirstOrDefaultAsync(dt => dt.UserId == userId && dt.Token == token);

                if (deviceToken != null)
                {
                    _context.DeviceTokens.Remove(deviceToken);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Device token unregistered for user {userId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to unregister device token for user {userId}");
                return false;
            }
        }

        public async Task<bool> DeactivateTokenAsync(string token)
        {
            try
            {
                var deviceToken = await _context.DeviceTokens
                    .FirstOrDefaultAsync(dt => dt.Token == token);

                if (deviceToken != null)
                {
                    deviceToken.IsActive = false;
                    deviceToken.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Device token deactivated");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate device token");
                return false;
            }
        }

        public async Task<bool> SendPushNotificationAsync(string userId, string title, string body, Dictionary<string, string>? data = null)
        {
            if (!_firebaseInitialized)
            {
                _logger.LogWarning("Firebase not initialized. Cannot send push notification.");
                return false;
            }

            try
            {
                // Get all active device tokens for the user
                var deviceTokens = await _context.DeviceTokens
                    .Where(dt => dt.UserId == userId && dt.IsActive)
                    .ToListAsync();

                if (!deviceTokens.Any())
                {
                    _logger.LogInformation($"No active device tokens found for user {userId}");
                    return false;
                }

                var successCount = 0;
                foreach (var deviceToken in deviceTokens)
                {
                    var success = await SendPushNotificationToTokenAsync(deviceToken.Token, title, body, data);
                    if (success)
                    {
                        successCount++;
                        // Update last used timestamp
                        deviceToken.LastUsedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Push notification sent to {successCount}/{deviceTokens.Count} devices for user {userId}");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send push notification to user {userId}");
                return false;
            }
        }

        public async Task<bool> SendPushNotificationToTokenAsync(string token, string title, string body, Dictionary<string, string>? data = null)
        {
            if (!_firebaseInitialized)
            {
                _logger.LogWarning("Firebase not initialized. Cannot send push notification.");
                return false;
            }

            try
            {
                var message = new FirebaseAdmin.Messaging.Message
                {
                    Token = token,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            Icon = "ic_notification",
                            Color = "#4CAF50",
                            Sound = "default",
                            ChannelId = "miniminds_notifications"
                        }
                    },
                    Apns = new ApnsConfig
                    {
                        Aps = new Aps
                        {
                            Sound = "default",
                            Badge = 1
                        }
                    }
                };

                if (data != null && data.Count > 0)
                {
                    message.Data = data;
                }

                var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation($"Push notification sent successfully: {response}");
                return true;
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogError(ex, $"Firebase messaging error: {ex.MessagingErrorCode}");

                // Handle invalid tokens
                if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                    ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                {
                    await DeactivateTokenAsync(token);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification to token");
                return false;
            }
        }

        public async Task<int> SendPushNotificationToMultipleAsync(List<string> userIds, string title, string body, Dictionary<string, string>? data = null)
        {
            if (!_firebaseInitialized)
            {
                _logger.LogWarning("Firebase not initialized. Cannot send push notifications.");
                return 0;
            }

            var successCount = 0;
            foreach (var userId in userIds)
            {
                var success = await SendPushNotificationAsync(userId, title, body, data);
                if (success) successCount++;
            }

            _logger.LogInformation($"Push notifications sent to {successCount}/{userIds.Count} users");
            return successCount;
        }
    }
}
