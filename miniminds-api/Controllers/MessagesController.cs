using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using DaycareAPI.Data;
using DaycareAPI.Models;
using DaycareAPI.DTOs;
using DaycareAPI.Hubs;
using DaycareAPI.Services;
using System.Security.Claims;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly INotificationService _notificationService;

        public MessagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<NotificationHub> hubContext, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _notificationService = notificationService;
        }

        private string GetCurrentUserId()
        {
            var userIdClaim = User.Claims
                .Where(c => c.Type == ClaimTypes.NameIdentifier)
                .LastOrDefault();
            
            return userIdClaim?.Value ?? "";
        }

        private async Task<bool> IsAdmin()
        {
            var userId = GetCurrentUserId();
            var user = await _userManager.FindByIdAsync(userId);
            return user != null && await _userManager.IsInRoleAsync(user, "Admin");
        }

        [HttpGet("inbox")]
        public async Task<ActionResult> GetInbox()
        {
            var currentUserId = GetCurrentUserId();

            // Get messages sent directly to me
            var directMessages = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Replies)
                .Where(m => (m.RecipientId == currentUserId || m.RecipientType == "all") && m.ParentMessageId == null)
                .OrderByDescending(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.SenderId,
                    SenderName = m.Sender!.FirstName + " " + m.Sender.LastName,
                    m.Subject,
                    m.Content,
                    m.SentAt,
                    m.IsRead,
                    m.RecipientType,
                    ReplyCount = m.Replies.Count,
                    HasNewReply = false
                })
                .ToListAsync();

            // Get my sent messages that have new replies (replies I haven't read)
            var messagesWithNewReplies = await _context.Messages
                .Include(m => m.Replies)
                    .ThenInclude(r => r.Sender)
                .Where(m => m.SenderId == currentUserId && m.ParentMessageId == null && m.Replies.Any(r => !r.IsRead && r.SenderId != currentUserId))
                .OrderByDescending(m => m.Replies.Where(r => !r.IsRead && r.SenderId != currentUserId).Max(r => r.SentAt))
                .Select(m => new
                {
                    m.Id,
                    SenderId = m.Replies.Where(r => !r.IsRead && r.SenderId != currentUserId).OrderByDescending(r => r.SentAt).First().SenderId,
                    SenderName = m.Replies.Where(r => !r.IsRead && r.SenderId != currentUserId).OrderByDescending(r => r.SentAt).First().Sender!.FirstName + " " + m.Replies.Where(r => !r.IsRead && r.SenderId != currentUserId).OrderByDescending(r => r.SentAt).First().Sender!.LastName,
                    Subject = "Re: " + m.Subject,
                    Content = m.Replies.Where(r => !r.IsRead && r.SenderId != currentUserId).OrderByDescending(r => r.SentAt).First().Content,
                    SentAt = m.Replies.Where(r => !r.IsRead && r.SenderId != currentUserId).Max(r => r.SentAt),
                    IsRead = false,
                    RecipientType = "individual",
                    ReplyCount = m.Replies.Count,
                    HasNewReply = true
                })
                .ToListAsync();

            // Combine and sort by date
            var allMessages = directMessages
                .Union(messagesWithNewReplies.Where(r => !directMessages.Any(d => d.Id == r.Id)))
                .OrderByDescending(m => m.SentAt)
                .ToList();

            return Ok(allMessages);
        }

        [HttpGet("sent")]
        public async Task<ActionResult> GetSent()
        {
            var currentUserId = GetCurrentUserId();
            
            var messages = await _context.Messages
                .Include(m => m.Recipient)
                .Include(m => m.Replies)
                .Where(m => m.SenderId == currentUserId && m.ParentMessageId == null)
                .OrderByDescending(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.RecipientId,
                    RecipientName = m.Recipient != null ? m.Recipient.FirstName + " " + m.Recipient.LastName : "All Users",
                    m.Subject,
                    m.Content,
                    m.SentAt,
                    m.RecipientType,
                    ReplyCount = m.Replies.Count
                })
                .ToListAsync();

            return Ok(messages);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetMessage(int id)
        {
            var currentUserId = GetCurrentUserId();
            
            var message = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Include(m => m.Replies)
                    .ThenInclude(r => r.Sender)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (message == null) return NotFound();
            
            if (message.RecipientId == currentUserId && !message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
                
                // Update message count via SignalR
                var unreadCount = await _context.Messages
                    .Where(m => (m.RecipientId == currentUserId || m.RecipientType == "all") && !m.IsRead)
                    .CountAsync();
                await _hubContext.Clients.Group($"User_{currentUserId}").SendAsync("ReceiveMessageCount", unreadCount);
            }

            return Ok(new
            {
                message.Id,
                message.SenderId,
                SenderName = message.Sender!.FirstName + " " + message.Sender.LastName,
                message.RecipientId,
                RecipientName = message.Recipient != null ? message.Recipient.FirstName + " " + message.Recipient.LastName : "All Users",
                message.Subject,
                message.Content,
                message.SentAt,
                message.IsRead,
                message.RecipientType,
                Replies = message.Replies.Select(r => new
                {
                    r.Id,
                    r.SenderId,
                    SenderName = r.Sender!.FirstName + " " + r.Sender.LastName,
                    r.Content,
                    r.SentAt
                }).OrderBy(r => r.SentAt)
            });
        }

        [HttpPost]
        public async Task<ActionResult> SendMessage([FromBody] SendMessageDto messageDto)
        {
            if (string.IsNullOrEmpty(messageDto.Content) || string.IsNullOrEmpty(messageDto.Subject))
            {
                return BadRequest(new { error = "Subject and Content are required" });
            }

            var currentUserId = GetCurrentUserId();
            var isAdmin = await IsAdmin();
            
            if (!isAdmin && messageDto.RecipientType != "individual")
            {
                return Forbid();
            }
            
            if (!isAdmin)
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                if (adminUsers.Count == 0)
                {
                    return BadRequest(new { error = "No admin available" });
                }
                messageDto.RecipientId = adminUsers.First().Id;
            }
            
            if (messageDto.RecipientType == "individual" && string.IsNullOrEmpty(messageDto.RecipientId))
            {
                return BadRequest(new { error = "RecipientId required for individual messages" });
            }
            
            var message = new Message
            {
                SenderId = currentUserId,
                RecipientId = messageDto.RecipientId,
                Subject = messageDto.Subject,
                Content = messageDto.Content,
                RecipientType = messageDto.RecipientType,
                ParentMessageId = messageDto.ParentMessageId,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var sender = await _userManager.FindByIdAsync(currentUserId);
            var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Someone";

            // If this is a reply, notify the original message sender
            if (messageDto.ParentMessageId.HasValue)
            {
                var parentMessage = await _context.Messages.FindAsync(messageDto.ParentMessageId.Value);
                if (parentMessage != null && parentMessage.SenderId != currentUserId)
                {
                    // Calculate unread count for the original sender (includes unread replies)
                    var directUnreadCount = await _context.Messages
                        .Where(m => (m.RecipientId == parentMessage.SenderId || m.RecipientType == "all") && !m.IsRead && m.ParentMessageId == null)
                        .CountAsync();

                    var replyUnreadCount = await _context.Messages
                        .Where(m => m.ParentMessageId != null && !m.IsRead && m.SenderId != parentMessage.SenderId)
                        .Join(_context.Messages.Where(p => p.SenderId == parentMessage.SenderId && p.ParentMessageId == null),
                            reply => reply.ParentMessageId,
                            parent => parent.Id,
                            (reply, parent) => reply)
                        .CountAsync();

                    var totalUnreadCount = directUnreadCount + replyUnreadCount;

                    await _hubContext.Clients.Group($"User_{parentMessage.SenderId}").SendAsync("ReceiveMessageCount", totalUnreadCount);
                    await _hubContext.Clients.Group($"User_{parentMessage.SenderId}").SendAsync("ReceiveNewMessage", new {
                        messageId = message.Id,
                        senderName = senderName,
                        subject = "Re: " + parentMessage.Subject,
                        isReply = true
                    });
                }
            }
            // Send SignalR notification to recipient for new messages
            else if (messageDto.RecipientType == "individual" && !string.IsNullOrEmpty(messageDto.RecipientId))
            {
                var unreadCount = await _context.Messages
                    .Where(m => (m.RecipientId == messageDto.RecipientId || m.RecipientType == "all") && !m.IsRead)
                    .CountAsync();

                await _hubContext.Clients.Group($"User_{messageDto.RecipientId}").SendAsync("ReceiveMessageCount", unreadCount);
                await _hubContext.Clients.Group($"User_{messageDto.RecipientId}").SendAsync("ReceiveNewMessage", new {
                    messageId = message.Id,
                    senderName = senderName,
                    subject = messageDto.Subject,
                    isReply = false
                });

                // Create notification for parent users when they receive a message
                var recipient = await _userManager.FindByIdAsync(messageDto.RecipientId);
                if (recipient != null && await _userManager.IsInRoleAsync(recipient, "Parent"))
                {
                    await _notificationService.SendNotificationAsync(
                        messageDto.RecipientId,
                        "New Message",
                        $"{senderName}: {messageDto.Subject}",
                        "Message",
                        $"/messages?id={message.Id}"
                    );
                }
            }
            else if (messageDto.RecipientType == "all")
            {
                var allUsers = await _userManager.Users.ToListAsync();
                foreach (var user in allUsers)
                {
                    var unreadCount = await _context.Messages
                        .Where(m => (m.RecipientId == user.Id || m.RecipientType == "all") && !m.IsRead)
                        .CountAsync();
                    await _hubContext.Clients.Group($"User_{user.Id}").SendAsync("ReceiveMessageCount", unreadCount);
                    await _hubContext.Clients.Group($"User_{user.Id}").SendAsync("ReceiveNewMessage", new {
                        messageId = message.Id,
                        senderName = senderName,
                        subject = messageDto.Subject,
                        isReply = false
                    });

                    // Create notification for parent users when they receive a broadcast message
                    if (await _userManager.IsInRoleAsync(user, "Parent"))
                    {
                        await _notificationService.SendNotificationAsync(
                            user.Id,
                            "New Message",
                            $"{senderName}: {messageDto.Subject}",
                            "Message",
                            $"/messages?id={message.Id}"
                        );
                    }
                }
            }

            return Ok(new { success = true, messageId = message.Id });
        }

        [HttpGet("recipients")]
        public async Task<ActionResult> GetRecipients()
        {
            var isAdmin = await IsAdmin();
            if (!isAdmin) return Forbid();
            
            var parents = await _userManager.GetUsersInRoleAsync("Parent");
            var teachers = await _userManager.GetUsersInRoleAsync("Teacher");
            
            return Ok(new
            {
                Parents = parents.Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, u.Email }),
                Teachers = teachers.Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, u.Email })
            });
        }
    }
}
