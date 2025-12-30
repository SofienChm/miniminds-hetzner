using DaycareAPI.Data;
using DaycareAPI.DTOs;
using DaycareAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChildrenController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ChildrenController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Children
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ChildListDto>>> GetChildren()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<Child> query;

            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId))
                {
                    query = _context.Children
                        .Where(c => c.ParentId == parentId)
                        .Include(c => c.Parent)
                        .OrderByDescending(c => c.CreatedAt);
                }
                else
                {
                    return Forbid();
                }
            }
            else
            {
                // Admin and Teacher can see all children
                query = _context.Children
                    .Include(c => c.Parent)
                    .OrderByDescending(c => c.CreatedAt);
            }

            var children = await query
                .Select(c => new ChildListDto
                {
                    Id = c.Id,
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    DateOfBirth = c.DateOfBirth,
                    Gender = c.Gender,
                    Allergies = c.Allergies,
                    MedicalNotes = c.MedicalNotes,
                    ParentId = c.ParentId,
                    EnrollmentDate = c.EnrollmentDate,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    HasProfilePicture = !string.IsNullOrEmpty(c.ProfilePicture),
                    Parent = c.Parent != null ? new ParentBasicDto
                    {
                        Id = c.Parent.Id,
                        FirstName = c.Parent.FirstName,
                        LastName = c.Parent.LastName,
                        Email = c.Parent.Email,
                        PhoneNumber = c.Parent.PhoneNumber
                    } : null
                })
                .ToListAsync();

            return children;
        }

        // GET: api/Children/5/profile-picture
        [HttpGet("{id}/profile-picture")]
        public async Task<ActionResult<object>> GetChildProfilePicture(int id)
        {
            var profilePicture = await _context.Children
                .Where(c => c.Id == id)
                .Select(c => c.ProfilePicture)
                .FirstOrDefaultAsync();

            if (profilePicture == null)
                return NotFound();

            return Ok(new { profilePicture });
        }

        // GET: api/Children/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Child>> GetChild(int id)
        {
            var child = await _context.Children
                .Include(c => c.Parent)
                .Include(c => c.ChildParents)
                    .ThenInclude(cp => cp.Parent)
                .Include(c => c.DailyActivities)
                .Include(c => c.Attendances)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (child == null)
                return NotFound();
                
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId) && child.ParentId != parentId)
                {
                    return Forbid();
                }
            }

            return child;
        }

        // GET: api/Children/ByParent/5
        [HttpGet("ByParent/{parentId}")]
        public async Task<ActionResult<IEnumerable<Child>>> GetChildrenByParent(int parentId)
        {
            return await _context.Children
                .Where(c => c.ParentId == parentId)
                .Include(c => c.Parent)
                .ToListAsync();
        }

        // POST: api/Children
        [HttpPost]
        public async Task<ActionResult<Child>> CreateChild(Child child)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Verify parent exists
            var parentExists = await _context.Parents.AnyAsync(p => p.Id == child.ParentId);
            if (!parentExists)
                return BadRequest(new { message = "Parent not found" });

            child.CreatedAt = DateTime.UtcNow;
            _context.Children.Add(child);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetChild), new { id = child.Id }, child);
        }

        // PUT: api/Children/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateChild(int id, Child child)
        {
            if (id != child.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            child.UpdatedAt = DateTime.UtcNow;
            _context.Entry(child).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ChildExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Children/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteChild(int id)
        {
            var child = await _context.Children.FindAsync(id);
            if (child == null)
                return NotFound();

            _context.Children.Remove(child);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ChildExists(int id)
        {
            return _context.Children.Any(e => e.Id == id);
        }

        // POST: api/Children/add-parent
        [HttpPost("add-parent")]
        public async Task<IActionResult> AddParentToChild([FromBody] AddParentRequest request)
        {
            var childParent = new ChildParent
            {
                ChildId = request.ChildId,
                ParentId = request.ParentId,
                RelationshipType = request.RelationshipType,
                IsPrimaryContact = request.IsPrimaryContact,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChildParents.Add(childParent);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // DELETE: api/Children/remove-parent/5
        [HttpDelete("remove-parent/{id}")]
        public async Task<IActionResult> RemoveParentFromChild(int id)
        {
            var childParent = await _context.ChildParents.FindAsync(id);
            if (childParent == null)
                return NotFound();

            _context.ChildParents.Remove(childParent);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/Children/5/toggle-status
        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleChildStatus(int id)
        {
            var child = await _context.Children.FindAsync(id);
            if (child == null)
                return NotFound();

            child.IsActive = !child.IsActive;
            child.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { isActive = child.IsActive });
        }
    }

    public class AddParentRequest
    {
        public int ChildId { get; set; }
        public int ParentId { get; set; }
        public string RelationshipType { get; set; } = "Parent";
        public bool IsPrimaryContact { get; set; }
    }
}