using DaycareAPI.Data;
using DaycareAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ActivityCommentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ActivityCommentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/ActivityComments/ByActivity/5
        [HttpGet("ByActivity/{activityId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetCommentsByActivity(int activityId)
        {
            // Verify activity exists and user has access
            var activity = await _context.DailyActivities
                .Include(a => a.Child)
                .FirstOrDefaultAsync(a => a.Id == activityId);

            if (activity == null)
                return NotFound(new { message = "Activity not found" });

            // Check parent access
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (!int.TryParse(parentIdClaim, out int parentId) ||
                    activity.Child == null ||
                    activity.Child.ParentId != parentId)
                {
                    return Forbid();
                }
            }

            // Get top-level comments with their replies
            var comments = await _context.ActivityComments
                .Where(c => c.ActivityId == activityId && c.ParentCommentId == null)
                .Include(c => c.User)
                .Include(c => c.Replies.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.User)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Content,
                    c.CreatedAt,
                    c.UpdatedAt,
                    User = new
                    {
                        Id = c.User != null ? c.User.Id : null,
                        FirstName = c.User != null ? c.User.FirstName : "Unknown",
                        LastName = c.User != null ? c.User.LastName : "",
                        ProfilePicture = c.User != null ? c.User.ProfilePicture : null
                    },
                    Replies = c.Replies.Select(r => new
                    {
                        r.Id,
                        r.Content,
                        r.CreatedAt,
                        r.UpdatedAt,
                        User = new
                        {
                            Id = r.User != null ? r.User.Id : null,
                            FirstName = r.User != null ? r.User.FirstName : "Unknown",
                            LastName = r.User != null ? r.User.LastName : "",
                            ProfilePicture = r.User != null ? r.User.ProfilePicture : null
                        }
                    }).OrderBy(r => r.CreatedAt).ToList()
                })
                .ToListAsync();

            return Ok(comments);
        }

        // POST: api/ActivityComments
        [HttpPost]
        public async Task<ActionResult<object>> CreateComment([FromBody] CreateCommentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Verify activity exists and user has access
            var activity = await _context.DailyActivities
                .Include(a => a.Child)
                .FirstOrDefaultAsync(a => a.Id == request.ActivityId);

            if (activity == null)
                return NotFound(new { message = "Activity not found" });

            // Check parent access
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (!int.TryParse(parentIdClaim, out int parentId) ||
                    activity.Child == null ||
                    activity.Child.ParentId != parentId)
                {
                    return Forbid();
                }
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Handle case where NameIdentifier might be email instead of ID
            if (!string.IsNullOrEmpty(userId) && userId.Contains("@"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userId);
                userId = user?.Id;
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // If replying, verify parent comment exists
            if (request.ParentCommentId.HasValue)
            {
                var parentComment = await _context.ActivityComments
                    .FirstOrDefaultAsync(c => c.Id == request.ParentCommentId.Value && c.ActivityId == request.ActivityId);
                if (parentComment == null)
                    return BadRequest(new { message = "Parent comment not found" });
            }

            var comment = new ActivityComment
            {
                ActivityId = request.ActivityId,
                Content = request.Content,
                UserId = userId,
                ParentCommentId = request.ParentCommentId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ActivityComments.Add(comment);
            await _context.SaveChangesAsync();

            // Reload with user info
            await _context.Entry(comment).Reference(c => c.User).LoadAsync();

            return CreatedAtAction(nameof(GetComment), new { id = comment.Id }, new
            {
                comment.Id,
                comment.Content,
                comment.CreatedAt,
                comment.ParentCommentId,
                User = new
                {
                    Id = comment.User?.Id,
                    FirstName = comment.User?.FirstName ?? "Unknown",
                    LastName = comment.User?.LastName ?? "",
                    ProfilePicture = comment.User?.ProfilePicture
                }
            });
        }

        // GET: api/ActivityComments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetComment(int id)
        {
            var comment = await _context.ActivityComments
                .Include(c => c.User)
                .Include(c => c.Activity)
                    .ThenInclude(a => a!.Child)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
                return NotFound();

            // Check parent access
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (!int.TryParse(parentIdClaim, out int parentId) ||
                    comment.Activity?.Child == null ||
                    comment.Activity.Child.ParentId != parentId)
                {
                    return Forbid();
                }
            }

            return Ok(new
            {
                comment.Id,
                comment.Content,
                comment.CreatedAt,
                comment.UpdatedAt,
                comment.ParentCommentId,
                User = new
                {
                    Id = comment.User?.Id,
                    FirstName = comment.User?.FirstName ?? "Unknown",
                    LastName = comment.User?.LastName ?? "",
                    ProfilePicture = comment.User?.ProfilePicture
                }
            });
        }

        // PUT: api/ActivityComments/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(int id, [FromBody] UpdateCommentRequest request)
        {
            var comment = await _context.ActivityComments
                .Include(c => c.Activity)
                    .ThenInclude(a => a!.Child)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
                return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Handle case where NameIdentifier might be email instead of ID
            if (!string.IsNullOrEmpty(userId) && userId.Contains("@"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userId);
                userId = user?.Id;
            }

            // Only allow owner to edit (or admin)
            if (comment.UserId != userId && userRole != "Admin")
                return Forbid();

            comment.Content = request.Content;
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/ActivityComments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.ActivityComments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
                return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Handle case where NameIdentifier might be email instead of ID
            if (!string.IsNullOrEmpty(userId) && userId.Contains("@"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userId);
                userId = user?.Id;
            }

            // Only allow owner to delete (or admin)
            if (comment.UserId != userId && userRole != "Admin")
                return Forbid();

            // Soft delete
            comment.IsDeleted = true;

            // Also soft delete all replies
            foreach (var reply in comment.Replies)
            {
                reply.IsDeleted = true;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateCommentRequest
    {
        public int ActivityId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? ParentCommentId { get; set; }
    }

    public class UpdateCommentRequest
    {
        public string Content { get; set; } = string.Empty;
    }
}
