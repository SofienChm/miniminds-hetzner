using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using DaycareAPI.Data;
using DaycareAPI.Models;
using DaycareAPI.DTOs;
using DaycareAPI.Hubs;
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

        public MessagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
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
            
            var messages = await _context.Messages
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
                    ReplyCount = m.Replies.Count
                })
                .ToListAsync();

            return Ok(messages);
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

            // Send SignalR notification to recipient
            if (messageDto.RecipientType == "individual" && !string.IsNullOrEmpty(messageDto.RecipientId))
            {
                var unreadCount = await _context.Messages
                    .Where(m => (m.RecipientId == messageDto.RecipientId || m.RecipientType == "all") && !m.IsRead)
                    .CountAsync();
                
                var sender = await _userManager.FindByIdAsync(currentUserId);
                var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Someone";
                
                await _hubContext.Clients.Group($"User_{messageDto.RecipientId}").SendAsync("ReceiveMessageCount", unreadCount);
                await _hubContext.Clients.Group($"User_{messageDto.RecipientId}").SendAsync("ReceiveNewMessage", new {
                    messageId = message.Id,
                    senderName = senderName,
                    subject = messageDto.Subject
                });
            }
            else if (messageDto.RecipientType == "all")
            {
                var sender = await _userManager.FindByIdAsync(currentUserId);
                var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Someone";
                
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
                        subject = messageDto.Subject
                    });
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
