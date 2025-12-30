using DaycareAPI.Data;
using DaycareAPI.Models;
using DaycareAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DeviceTokensController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<DeviceTokensController> _logger;

        public DeviceTokensController(
            ApplicationDbContext context,
            IPushNotificationService pushNotificationService,
            ILogger<DeviceTokensController> logger)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
        }

        public class RegisterTokenRequest
        {
            public string Token { get; set; } = string.Empty;
            public string Platform { get; set; } = string.Empty; // "android", "ios", "web"
            public string? DeviceModel { get; set; }
        }

        public class UnregisterTokenRequest
        {
            public string Token { get; set; } = string.Empty;
        }

        /// <summary>
        /// Register a device token for push notifications
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult> RegisterToken([FromBody] RegisterTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { message = "Token is required" });
                }

                if (string.IsNullOrEmpty(request.Platform))
                {
                    return BadRequest(new { message = "Platform is required" });
                }

                var validPlatforms = new[] { "android", "ios", "web" };
                if (!validPlatforms.Contains(request.Platform.ToLower()))
                {
                    return BadRequest(new { message = "Invalid platform. Must be 'android', 'ios', or 'web'" });
                }

                var deviceToken = await _pushNotificationService.RegisterDeviceTokenAsync(
                    userId,
                    request.Token,
                    request.Platform.ToLower(),
                    request.DeviceModel
                );

                if (deviceToken == null)
                {
                    return StatusCode(500, new { message = "Failed to register device token" });
                }

                _logger.LogInformation($"Device token registered for user {userId} on {request.Platform}");

                return Ok(new
                {
                    message = "Device token registered successfully",
                    tokenId = deviceToken.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering device token");
                return StatusCode(500, new { message = "An error occurred while registering device token" });
            }
        }

        /// <summary>
        /// Unregister a device token (e.g., on logout)
        /// </summary>
        [HttpPost("unregister")]
        public async Task<ActionResult> UnregisterToken([FromBody] UnregisterTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { message = "Token is required" });
                }

                var result = await _pushNotificationService.UnregisterDeviceTokenAsync(userId, request.Token);

                if (result)
                {
                    _logger.LogInformation($"Device token unregistered for user {userId}");
                    return Ok(new { message = "Device token unregistered successfully" });
                }

                return NotFound(new { message = "Device token not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering device token");
                return StatusCode(500, new { message = "An error occurred while unregistering device token" });
            }
        }

        /// <summary>
        /// Get all device tokens for the current user
        /// </summary>
        [HttpGet("my-tokens")]
        public async Task<ActionResult> GetMyTokens()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var tokens = await _context.DeviceTokens
                    .Where(dt => dt.UserId == userId)
                    .Select(dt => new
                    {
                        dt.Id,
                        dt.Platform,
                        dt.DeviceModel,
                        dt.IsActive,
                        dt.CreatedAt,
                        dt.LastUsedAt
                    })
                    .ToListAsync();

                return Ok(tokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device tokens");
                return StatusCode(500, new { message = "An error occurred while getting device tokens" });
            }
        }

        /// <summary>
        /// Send a test push notification to the current user
        /// </summary>
        [HttpPost("test")]
        public async Task<ActionResult> SendTestNotification()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var success = await _pushNotificationService.SendPushNotificationAsync(
                    userId,
                    "Test Notification",
                    "This is a test push notification from MiniMinds!",
                    new Dictionary<string, string>
                    {
                        { "type", "test" },
                        { "redirectUrl", "/notifications" }
                    }
                );

                if (success)
                {
                    return Ok(new { message = "Test notification sent successfully" });
                }

                return BadRequest(new { message = "Failed to send test notification. Make sure you have registered device tokens." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test notification");
                return StatusCode(500, new { message = "An error occurred while sending test notification" });
            }
        }
    }
}
