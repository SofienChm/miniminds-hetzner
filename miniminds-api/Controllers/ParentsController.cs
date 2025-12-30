using DaycareAPI.Data;
using DaycareAPI.DTOs;
using DaycareAPI.Models;
using DaycareAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class ToggleParentStatusRequest
{
    public bool DeactivateChildren { get; set; }
}

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ParentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public ParentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        // GET: api/Parents
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ParentListDto>>> GetParents([FromQuery] string? search = null)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Only Admin and Teacher can see all parents
            if (userRole != "Admin" && userRole != "Teacher")
            {
                return Forbid();
            }

            var query = _context.Parents.Include(p => p.Children).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.FirstName.Contains(search) ||
                    p.LastName.Contains(search) ||
                    p.Email.Contains(search) ||
                    p.PhoneNumber.Contains(search));
            }

            var parents = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ParentListDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Email = p.Email,
                    PhoneNumber = p.PhoneNumber,
                    Address = p.Address,
                    EmergencyContact = p.EmergencyContact,
                    Gender = p.Gender,
                    DateOfBirth = p.DateOfBirth,
                    Work = p.Work,
                    ZipCode = p.ZipCode,
                    ParentType = p.ParentType,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    HasProfilePicture = !string.IsNullOrEmpty(p.ProfilePicture),
                    Children = p.Children.Select(c => new ChildListDto
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
                        HasProfilePicture = !string.IsNullOrEmpty(c.ProfilePicture)
                    }).ToList()
                })
                .ToListAsync();

            return parents;
        }

        // GET: api/Parents/5/profile-picture
        [HttpGet("{id}/profile-picture")]
        public async Task<ActionResult<object>> GetParentProfilePicture(int id)
        {
            var profilePicture = await _context.Parents
                .Where(p => p.Id == id)
                .Select(p => p.ProfilePicture)
                .FirstOrDefaultAsync();

            if (profilePicture == null)
                return NotFound();

            return Ok(new { profilePicture });
        }

        // GET: api/Parents/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Parent>> GetParent(int id)
        {
            var parent = await _context.Parents
                .Include(p => p.Children)
                .Include(p => p.ChildParents)
                    .ThenInclude(cp => cp.Child)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (parent == null)
                return NotFound();

            // Combine children from both direct relationship and ChildParent relationships
            var allChildren = new List<Child>();
            
            // Add direct children
            if (parent.Children != null)
            {
                allChildren.AddRange(parent.Children);
            }
            
            // Add children from ChildParent relationships (avoid duplicates)
            if (parent.ChildParents != null)
            {
                var childParentChildren = parent.ChildParents
                    .Where(cp => cp.Child != null && !allChildren.Any(c => c.Id == cp.Child!.Id))
                    .Select(cp => cp.Child!)
                    .ToList();
                allChildren.AddRange(childParentChildren);
            }
            
            // Update the parent's children collection
            parent.Children = allChildren;

            return parent;
        }

        // POST: api/Parents
        [HttpPost]
        public async Task<ActionResult<Parent>> CreateParent(CreateParentDto parentDto)
        {
            Console.WriteLine($"Received CreateParent request for: {parentDto.Email}");
            Console.WriteLine($"Password provided: {!string.IsNullOrEmpty(parentDto.Password)}");
            
            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState is invalid:");
                foreach (var error in ModelState)
                {
                    Console.WriteLine($"{error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                }
                return BadRequest(ModelState);
            }

            // Check if user with this email already exists
            var existingUser = await _userManager.FindByEmailAsync(parentDto.Email);
            if (existingUser != null)
            {
                Console.WriteLine($"User with email {parentDto.Email} already exists");
                return BadRequest(new { message = "A user with this email already exists" });
            }

            // Create user account
            var user = new ApplicationUser
            {
                UserName = parentDto.Email,
                Email = parentDto.Email,
                FirstName = parentDto.FirstName,
                LastName = parentDto.LastName,
                ProfilePicture = parentDto.ProfilePicture,
                CreatedAt = DateTime.UtcNow
            };

            Console.WriteLine($"Creating user account for: {user.Email}");
            var userResult = await _userManager.CreateAsync(user, parentDto.Password);
            if (!userResult.Succeeded)
            {
                Console.WriteLine($"User creation failed: {string.Join(", ", userResult.Errors.Select(e => e.Description))}");
                return BadRequest(userResult.Errors);
            }
            Console.WriteLine($"User created successfully: {user.Id}");

            // Assign Parent role
            Console.WriteLine($"Assigning Parent role to user: {user.Id}");
            var roleResult = await _userManager.AddToRoleAsync(user, "Parent");
            if (!roleResult.Succeeded)
            {
                Console.WriteLine($"Role assignment failed: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                // If role assignment fails, delete the user
                await _userManager.DeleteAsync(user);
                return BadRequest(roleResult.Errors);
            }
            Console.WriteLine($"Role assigned successfully");

            // Create parent record
            var parent = new Parent
            {
                FirstName = parentDto.FirstName,
                LastName = parentDto.LastName,
                Email = parentDto.Email,
                PhoneNumber = parentDto.PhoneNumber,
                Address = parentDto.Address,
                EmergencyContact = parentDto.EmergencyContact,
                ProfilePicture = parentDto.ProfilePicture,
                Gender = parentDto.Gender,
                DateOfBirth = parentDto.DateOfBirth,
                Work = parentDto.Work,
                ZipCode = parentDto.ZipCode,
                ParentType = parentDto.ParentType,
                CreatedAt = DateTime.UtcNow
            };

            Console.WriteLine($"Creating parent record for: {parent.Email}");
            _context.Parents.Add(parent);
            await _context.SaveChangesAsync();
            Console.WriteLine($"Parent record created successfully: {parent.Id}");

            // Send welcome email
            try
            {
                await _emailService.SendWelcomeEmailAsync(parent.Email, $"{parent.FirstName} {parent.LastName}");
                Console.WriteLine($"Welcome email sent to: {parent.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send welcome email: {ex.Message}");
            }

            return CreatedAtAction(nameof(GetParent), new { id = parent.Id }, parent);
        }

        // PUT: api/Parents/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateParent(int id, Parent parent)
        {
            if (id != parent.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Get the existing parent to find the email for syncing to ApplicationUser
            var existingParent = await _context.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (existingParent == null)
                return NotFound();

            parent.UpdatedAt = DateTime.UtcNow;
            _context.Entry(parent).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                // Sync changes to ApplicationUser
                var user = await _userManager.FindByEmailAsync(existingParent.Email);
                if (user != null)
                {
                    user.FirstName = parent.FirstName;
                    user.LastName = parent.LastName;
                    user.Email = parent.Email;
                    user.UserName = parent.Email;
                    if (!string.IsNullOrEmpty(parent.ProfilePicture))
                        user.ProfilePicture = parent.ProfilePicture;
                    user.UpdatedAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ParentExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Parents/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteParent(int id)
        {
            var parent = await _context.Parents.FindAsync(id);
            if (parent == null)
                return NotFound();

            _context.Parents.Remove(parent);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ParentExists(int id)
        {
            return _context.Parents.Any(e => e.Id == id);
        }

        // POST: api/Parents/sync-profile-pictures
        // One-time endpoint to sync profile pictures from ApplicationUser to Parent
        [HttpPost("sync-profile-pictures")]
        public async Task<IActionResult> SyncProfilePictures()
        {
            var parents = await _context.Parents.ToListAsync();
            var syncedCount = 0;

            foreach (var parent in parents)
            {
                var user = await _userManager.FindByEmailAsync(parent.Email);
                if (user != null && !string.IsNullOrEmpty(user.ProfilePicture))
                {
                    parent.ProfilePicture = user.ProfilePicture;
                    parent.UpdatedAt = DateTime.UtcNow;
                    syncedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Synced {syncedCount} parent profile pictures" });
        }

        // PUT: api/Parents/5/toggle-status
        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleParentStatus(int id, [FromBody] ToggleParentStatusRequest request)
        {
            var parent = await _context.Parents
                .Include(p => p.Children)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (parent == null)
                return NotFound();

            parent.IsActive = !parent.IsActive;
            parent.UpdatedAt = DateTime.UtcNow;

            // If deactivating parent and user chose to deactivate children
            if (!parent.IsActive && request.DeactivateChildren)
            {
                foreach (var child in parent.Children)
                {
                    child.IsActive = false;
                    child.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { isActive = parent.IsActive });
        }
    }
}