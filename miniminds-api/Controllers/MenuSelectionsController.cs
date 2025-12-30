using DaycareAPI.Data;
using DaycareAPI.Models;
using DaycareAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MenuSelectionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MenuSelectionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/MenuSelections/child/5/menu/10
        [HttpGet("child/{childId}/menu/{menuId}")]
        public async Task<ActionResult<ChildMenuViewDto>> GetChildMenuSelections(int childId, int menuId)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Verify parent has access to this child
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (!int.TryParse(parentIdClaim, out int parentId))
                    return Forbid();

                var hasAccess = await _context.Children
                    .AnyAsync(c => c.Id == childId && c.ParentId == parentId);

                if (!hasAccess)
                {
                    // Check ChildParents table for secondary parents
                    hasAccess = await _context.ChildParents
                        .AnyAsync(cp => cp.ChildId == childId && cp.ParentId == parentId);
                }

                if (!hasAccess)
                    return Forbid();
            }

            var child = await _context.Children.FindAsync(childId);
            if (child == null)
                return NotFound(new { message = "Child not found" });

            var menu = await _context.Menus
                .Include(m => m.MenuItems)
                    .ThenInclude(mi => mi.FoodItem)
                .FirstOrDefaultAsync(m => m.Id == menuId);

            if (menu == null)
                return NotFound(new { message = "Menu not found" });

            if (userRole == "Parent" && !menu.IsPublished)
                return NotFound(new { message = "Menu not available" });

            var selections = await _context.MenuSelections
                .Where(ms => ms.ChildId == childId && ms.MenuId == menuId)
                .ToListAsync();

            // Check for allergy warnings
            var allergyWarnings = new List<string>();
            if (!string.IsNullOrEmpty(child.Allergies))
            {
                var childAllergens = child.Allergies.Split(',')
                    .Select(a => a.Trim().ToLower())
                    .ToList();

                foreach (var item in menu.MenuItems.Where(mi => mi.FoodItem != null))
                {
                    if (!string.IsNullOrEmpty(item.FoodItem!.Allergens))
                    {
                        var foodAllergens = item.FoodItem.Allergens.Split(',')
                            .Select(a => a.Trim().ToLower())
                            .ToList();

                        var matches = childAllergens.Intersect(foodAllergens).ToList();
                        if (matches.Any())
                        {
                            allergyWarnings.Add($"⚠️ {item.FoodItem.Name} contains: {string.Join(", ", matches)}");
                        }
                    }
                }
            }

            return Ok(new ChildMenuViewDto
            {
                ChildId = child.Id,
                ChildName = $"{child.FirstName} {child.LastName}",
                Allergies = child.Allergies,
                Menu = MapToMenuWithItemsDto(menu),
                Selections = selections.Select(s => new MenuSelectionDto
                {
                    Id = s.Id,
                    MenuItemId = s.MenuItemId,
                    IsSelected = s.IsSelected,
                    Notes = s.Notes,
                    SelectionStatus = s.SelectionStatus,
                    CreatedAt = s.CreatedAt
                }).ToList(),
                AllergyWarnings = allergyWarnings
            });
        }

        // GET: api/MenuSelections/menu/10
        [HttpGet("menu/{menuId}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<IEnumerable<object>>> GetMenuSelections(int menuId)
        {
            var selections = await _context.MenuSelections
                .Include(ms => ms.Child)
                .Include(ms => ms.MenuItem)
                    .ThenInclude(mi => mi!.FoodItem)
                .Include(ms => ms.Parent)
                .Where(ms => ms.MenuId == menuId)
                .OrderBy(ms => ms.Child!.FirstName)
                .ThenBy(ms => ms.MenuItem!.MealType)
                .ToListAsync();

            var result = selections.Select(s => new
            {
                s.Id,
                s.ChildId,
                ChildName = s.Child != null ? $"{s.Child.FirstName} {s.Child.LastName}" : "Unknown",
                ChildAllergies = s.Child?.Allergies,
                s.MenuItemId,
                MealType = s.MenuItem?.MealType,
                FoodItemName = s.MenuItem?.FoodItem?.Name,
                s.IsSelected,
                s.Notes,
                s.SelectionStatus,
                ParentName = s.Parent != null ? $"{s.Parent.FirstName} {s.Parent.LastName}" : "Unknown",
                s.CreatedAt
            });

            return Ok(result);
        }

        // POST: api/MenuSelections
        [HttpPost]
        public async Task<ActionResult<MenuSelection>> CreateSelection(CreateMenuSelectionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var parentIdClaim = User.FindFirst("ParentId")?.Value;

            int parentId;
            if (userRole == "Parent")
            {
                if (!int.TryParse(parentIdClaim, out parentId))
                    return Forbid();

                // Verify parent has access to this child
                var hasAccess = await _context.Children
                    .AnyAsync(c => c.Id == dto.ChildId && c.ParentId == parentId);

                if (!hasAccess)
                {
                    hasAccess = await _context.ChildParents
                        .AnyAsync(cp => cp.ChildId == dto.ChildId && cp.ParentId == parentId);
                }

                if (!hasAccess)
                    return Forbid();
            }
            else
            {
                // Admin/Teacher can make selections on behalf of parents
                parentId = dto.ChildId; // This will be overwritten below with actual parent
                var child = await _context.Children.FindAsync(dto.ChildId);
                if (child != null)
                {
                    parentId = child.ParentId;
                }
            }

            // Check if menu is published
            var menu = await _context.Menus.FindAsync(dto.MenuId);
            if (menu == null)
                return NotFound(new { message = "Menu not found" });

            if (userRole == "Parent" && !menu.IsPublished)
                return BadRequest(new { message = "Menu is not available for selection" });

            // Check if selection already exists
            var existingSelection = await _context.MenuSelections
                .FirstOrDefaultAsync(ms =>
                    ms.ChildId == dto.ChildId &&
                    ms.MenuId == dto.MenuId &&
                    ms.MenuItemId == dto.MenuItemId);

            if (existingSelection != null)
            {
                // Update existing selection
                existingSelection.IsSelected = dto.IsSelected;
                existingSelection.Notes = dto.Notes;
                existingSelection.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(existingSelection);
            }

            var selection = new MenuSelection
            {
                ChildId = dto.ChildId,
                MenuId = dto.MenuId,
                MenuItemId = dto.MenuItemId,
                ParentId = parentId,
                IsSelected = dto.IsSelected,
                Notes = dto.Notes,
                SelectionStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.MenuSelections.Add(selection);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetChildMenuSelections),
                new { childId = dto.ChildId, menuId = dto.MenuId }, selection);
        }

        // POST: api/MenuSelections/bulk
        [HttpPost("bulk")]
        public async Task<ActionResult> CreateBulkSelections(BulkMenuSelectionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var parentIdClaim = User.FindFirst("ParentId")?.Value;

            int parentId;
            if (userRole == "Parent")
            {
                if (!int.TryParse(parentIdClaim, out parentId))
                    return Forbid();

                var hasAccess = await _context.Children
                    .AnyAsync(c => c.Id == dto.ChildId && c.ParentId == parentId);

                if (!hasAccess)
                {
                    hasAccess = await _context.ChildParents
                        .AnyAsync(cp => cp.ChildId == dto.ChildId && cp.ParentId == parentId);
                }

                if (!hasAccess)
                    return Forbid();
            }
            else
            {
                var child = await _context.Children.FindAsync(dto.ChildId);
                if (child == null)
                    return NotFound(new { message = "Child not found" });
                parentId = child.ParentId;
            }

            var menu = await _context.Menus.FindAsync(dto.MenuId);
            if (menu == null)
                return NotFound(new { message = "Menu not found" });

            if (userRole == "Parent" && !menu.IsPublished)
                return BadRequest(new { message = "Menu is not available for selection" });

            // Remove existing selections for this child/menu combo
            var existingSelections = await _context.MenuSelections
                .Where(ms => ms.ChildId == dto.ChildId && ms.MenuId == dto.MenuId)
                .ToListAsync();

            _context.MenuSelections.RemoveRange(existingSelections);

            // Add new selections
            foreach (var item in dto.Selections)
            {
                var selection = new MenuSelection
                {
                    ChildId = dto.ChildId,
                    MenuId = dto.MenuId,
                    MenuItemId = item.MenuItemId,
                    ParentId = parentId,
                    IsSelected = item.IsSelected,
                    Notes = item.Notes,
                    SelectionStatus = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                _context.MenuSelections.Add(selection);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Selections saved successfully" });
        }

        // PUT: api/MenuSelections/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSelection(int id, UpdateMenuSelectionDto dto)
        {
            var selection = await _context.MenuSelections
                .Include(ms => ms.Child)
                .FirstOrDefaultAsync(ms => ms.Id == id);

            if (selection == null)
                return NotFound();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (!int.TryParse(parentIdClaim, out int parentId))
                    return Forbid();

                if (selection.ParentId != parentId)
                    return Forbid();

                // Parents can only update isSelected and notes, not status
                selection.IsSelected = dto.IsSelected;
                selection.Notes = dto.Notes;
            }
            else
            {
                // Admin/Teacher can update everything
                selection.IsSelected = dto.IsSelected;
                selection.Notes = dto.Notes;
                selection.SelectionStatus = dto.SelectionStatus;
            }

            selection.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/MenuSelections/5/status
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdateSelectionStatus(int id, [FromBody] string status)
        {
            var selection = await _context.MenuSelections.FindAsync(id);
            if (selection == null)
                return NotFound();

            var validStatuses = new[] { "Pending", "Confirmed", "Served" };
            if (!validStatuses.Contains(status))
                return BadRequest(new { message = "Invalid status" });

            selection.SelectionStatus = status;
            selection.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Status updated to {status}" });
        }

        // DELETE: api/MenuSelections/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSelection(int id)
        {
            var selection = await _context.MenuSelections.FindAsync(id);
            if (selection == null)
                return NotFound();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (!int.TryParse(parentIdClaim, out int parentId))
                    return Forbid();

                if (selection.ParentId != parentId)
                    return Forbid();
            }

            _context.MenuSelections.Remove(selection);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/MenuSelections/report/menu/5
        [HttpGet("report/menu/{menuId}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<object>> GetMenuReport(int menuId)
        {
            var menu = await _context.Menus
                .Include(m => m.MenuItems)
                    .ThenInclude(mi => mi.FoodItem)
                .FirstOrDefaultAsync(m => m.Id == menuId);

            if (menu == null)
                return NotFound();

            var selections = await _context.MenuSelections
                .Include(ms => ms.Child)
                .Include(ms => ms.MenuItem)
                    .ThenInclude(mi => mi!.FoodItem)
                .Where(ms => ms.MenuId == menuId)
                .ToListAsync();

            var report = new
            {
                MenuId = menu.Id,
                MenuName = menu.Name,
                MenuDate = menu.MenuDate,
                TotalChildren = selections.Select(s => s.ChildId).Distinct().Count(),
                MealBreakdown = menu.MenuItems.Select(mi => new
                {
                    MenuItem = mi.FoodItem?.Name,
                    MealType = mi.MealType,
                    TotalSelected = selections.Count(s => s.MenuItemId == mi.Id && s.IsSelected),
                    TotalDeclined = selections.Count(s => s.MenuItemId == mi.Id && !s.IsSelected),
                    Pending = selections.Count(s => s.MenuItemId == mi.Id && s.SelectionStatus == "Pending"),
                    Confirmed = selections.Count(s => s.MenuItemId == mi.Id && s.SelectionStatus == "Confirmed"),
                    Served = selections.Count(s => s.MenuItemId == mi.Id && s.SelectionStatus == "Served")
                }).ToList(),
                ChildrenWithAllergies = selections
                    .Where(s => !string.IsNullOrEmpty(s.Child?.Allergies))
                    .Select(s => new
                    {
                        ChildName = $"{s.Child?.FirstName} {s.Child?.LastName}",
                        Allergies = s.Child?.Allergies
                    })
                    .Distinct()
                    .ToList()
            };

            return Ok(report);
        }

        private MenuWithItemsDto MapToMenuWithItemsDto(Menu menu)
        {
            return new MenuWithItemsDto
            {
                Id = menu.Id,
                Name = menu.Name,
                Description = menu.Description,
                MenuDate = menu.MenuDate,
                MenuType = menu.MenuType,
                IsPublished = menu.IsPublished,
                IsTemplate = menu.IsTemplate,
                Notes = menu.Notes,
                MeetsGrainRequirement = menu.MeetsGrainRequirement,
                MeetsProteinRequirement = menu.MeetsProteinRequirement,
                MeetsDairyRequirement = menu.MeetsDairyRequirement,
                MeetsFruitVegRequirement = menu.MeetsFruitVegRequirement,
                CreatedAt = menu.CreatedAt,
                MenuItems = menu.MenuItems
                    .OrderBy(mi => mi.MealType switch
                    {
                        "Breakfast" => 1,
                        "AM Snack" => 2,
                        "Lunch" => 3,
                        "PM Snack" => 4,
                        "Dinner" => 5,
                        _ => 6
                    })
                    .ThenBy(mi => mi.DisplayOrder)
                    .Select(mi => new MenuItemWithFoodDto
                    {
                        Id = mi.Id,
                        MealType = mi.MealType,
                        ServingSize = mi.ServingSize,
                        DisplayOrder = mi.DisplayOrder,
                        Notes = mi.Notes,
                        FoodItem = mi.FoodItem != null ? new FoodItemDto
                        {
                            Id = mi.FoodItem.Id,
                            Name = mi.FoodItem.Name,
                            Description = mi.FoodItem.Description,
                            Category = mi.FoodItem.Category,
                            Calories = mi.FoodItem.Calories,
                            Protein = mi.FoodItem.Protein,
                            Carbohydrates = mi.FoodItem.Carbohydrates,
                            Fat = mi.FoodItem.Fat,
                            Fiber = mi.FoodItem.Fiber,
                            Sugar = mi.FoodItem.Sugar,
                            Allergens = mi.FoodItem.Allergens,
                            DietaryTags = mi.FoodItem.DietaryTags,
                            ImageUrl = mi.FoodItem.ImageUrl,
                            IsActive = mi.FoodItem.IsActive
                        } : new FoodItemDto()
                    }).ToList()
            };
        }
    }
}
