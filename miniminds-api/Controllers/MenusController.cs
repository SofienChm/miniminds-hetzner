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
    public class MenusController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MenusController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Menus
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MenuWithItemsDto>>> GetMenus(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? menuType = null,
            [FromQuery] bool? publishedOnly = null,
            [FromQuery] bool? templatesOnly = null)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var query = _context.Menus
                .Include(m => m.MenuItems)
                    .ThenInclude(mi => mi.FoodItem)
                .Include(m => m.CreatedBy)
                .AsQueryable();

            // Parents only see published menus
            if (userRole == "Parent")
            {
                query = query.Where(m => m.IsPublished);
            }
            else if (publishedOnly == true)
            {
                query = query.Where(m => m.IsPublished);
            }

            if (templatesOnly == true)
            {
                query = query.Where(m => m.IsTemplate);
            }

            if (startDate.HasValue)
            {
                query = query.Where(m => m.MenuDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(m => m.MenuDate <= endDate.Value.Date);
            }

            if (!string.IsNullOrEmpty(menuType))
            {
                query = query.Where(m => m.MenuType == menuType);
            }

            var menus = await query.OrderByDescending(m => m.MenuDate).ToListAsync();

            return Ok(menus.Select(m => MapToMenuWithItemsDto(m)));
        }

        // GET: api/Menus/5
        [HttpGet("{id}")]
        public async Task<ActionResult<MenuWithItemsDto>> GetMenu(int id)
        {
            var menu = await _context.Menus
                .Include(m => m.MenuItems)
                    .ThenInclude(mi => mi.FoodItem)
                .Include(m => m.CreatedBy)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menu == null)
                return NotFound();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent" && !menu.IsPublished)
            {
                return Forbid();
            }

            return Ok(MapToMenuWithItemsDto(menu));
        }

        // GET: api/Menus/ByDate?date=2024-12-03
        [HttpGet("ByDate")]
        public async Task<ActionResult<MenuWithItemsDto>> GetMenuByDate([FromQuery] DateTime date)
        {
            var menu = await _context.Menus
                .Include(m => m.MenuItems)
                    .ThenInclude(mi => mi.FoodItem)
                .Include(m => m.CreatedBy)
                .FirstOrDefaultAsync(m => m.MenuDate.Date == date.Date && !m.IsTemplate);

            if (menu == null)
                return NotFound(new { message = "No menu found for this date" });

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent" && !menu.IsPublished)
            {
                return NotFound(new { message = "No menu published for this date" });
            }

            return Ok(MapToMenuWithItemsDto(menu));
        }

        // GET: api/Menus/Week?startDate=2024-12-02
        [HttpGet("Week")]
        public async Task<ActionResult<IEnumerable<MenuWithItemsDto>>> GetWeekMenus([FromQuery] DateTime startDate)
        {
            var endDate = startDate.Date.AddDays(7);
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var query = _context.Menus
                .Include(m => m.MenuItems)
                    .ThenInclude(mi => mi.FoodItem)
                .Include(m => m.CreatedBy)
                .Where(m => m.MenuDate >= startDate.Date && m.MenuDate < endDate && !m.IsTemplate);

            if (userRole == "Parent")
            {
                query = query.Where(m => m.IsPublished);
            }

            var menus = await query.OrderBy(m => m.MenuDate).ToListAsync();

            return Ok(menus.Select(m => MapToMenuWithItemsDto(m)));
        }

        // POST: api/Menus
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<Menu>> CreateMenu(CreateMenuDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var teacherIdClaim = User.FindFirst("TeacherId")?.Value;
            int? teacherId = null;
            if (int.TryParse(teacherIdClaim, out int tid))
            {
                teacherId = tid;
            }

            var menu = new Menu
            {
                Name = dto.Name,
                Description = dto.Description,
                MenuDate = dto.MenuDate.Date,
                MenuType = dto.MenuType,
                IsTemplate = dto.IsTemplate,
                Notes = dto.Notes,
                CreatedById = teacherId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Menus.Add(menu);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMenu), new { id = menu.Id }, menu);
        }

        // PUT: api/Menus/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdateMenu(int id, UpdateMenuDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var menu = await _context.Menus.FindAsync(id);
            if (menu == null)
                return NotFound();

            menu.Name = dto.Name;
            menu.Description = dto.Description;
            menu.MenuDate = dto.MenuDate.Date;
            menu.MenuType = dto.MenuType;
            menu.IsTemplate = dto.IsTemplate;
            menu.IsPublished = dto.IsPublished;
            menu.Notes = dto.Notes;
            menu.MeetsGrainRequirement = dto.MeetsGrainRequirement;
            menu.MeetsProteinRequirement = dto.MeetsProteinRequirement;
            menu.MeetsDairyRequirement = dto.MeetsDairyRequirement;
            menu.MeetsFruitVegRequirement = dto.MeetsFruitVegRequirement;
            menu.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MenuExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // PUT: api/Menus/5/publish
        [HttpPut("{id}/publish")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> PublishMenu(int id)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null)
                return NotFound();

            menu.IsPublished = true;
            menu.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Menu published successfully" });
        }

        // PUT: api/Menus/5/unpublish
        [HttpPut("{id}/unpublish")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UnpublishMenu(int id)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null)
                return NotFound();

            menu.IsPublished = false;
            menu.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Menu unpublished" });
        }

        // POST: api/Menus/duplicate
        [HttpPost("duplicate")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<Menu>> DuplicateMenu(DuplicateMenuDto dto)
        {
            var sourceMenu = await _context.Menus
                .Include(m => m.MenuItems)
                .FirstOrDefaultAsync(m => m.Id == dto.SourceMenuId);

            if (sourceMenu == null)
                return NotFound(new { message = "Source menu not found" });

            var teacherIdClaim = User.FindFirst("TeacherId")?.Value;
            int? teacherId = null;
            if (int.TryParse(teacherIdClaim, out int tid))
            {
                teacherId = tid;
            }

            var newMenu = new Menu
            {
                Name = dto.NewName ?? $"{sourceMenu.Name} (Copy)",
                Description = sourceMenu.Description,
                MenuDate = dto.NewMenuDate.Date,
                MenuType = sourceMenu.MenuType,
                IsTemplate = false,
                IsPublished = false,
                Notes = sourceMenu.Notes,
                MeetsGrainRequirement = sourceMenu.MeetsGrainRequirement,
                MeetsProteinRequirement = sourceMenu.MeetsProteinRequirement,
                MeetsDairyRequirement = sourceMenu.MeetsDairyRequirement,
                MeetsFruitVegRequirement = sourceMenu.MeetsFruitVegRequirement,
                CreatedById = teacherId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Menus.Add(newMenu);
            await _context.SaveChangesAsync();

            // Duplicate menu items
            foreach (var item in sourceMenu.MenuItems)
            {
                var newItem = new MenuItem
                {
                    MenuId = newMenu.Id,
                    FoodItemId = item.FoodItemId,
                    MealType = item.MealType,
                    ServingSize = item.ServingSize,
                    DisplayOrder = item.DisplayOrder,
                    Notes = item.Notes,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MenuItems.Add(newItem);
            }
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMenu), new { id = newMenu.Id }, newMenu);
        }

        // DELETE: api/Menus/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMenu(int id)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null)
                return NotFound();

            _context.Menus.Remove(menu);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Menu Items Endpoints

        // POST: api/Menus/5/items
        [HttpPost("{menuId}/items")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<MenuItem>> AddMenuItem(int menuId, CreateMenuItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.MenuId != menuId)
                return BadRequest(new { message = "Menu ID mismatch" });

            var menuExists = await _context.Menus.AnyAsync(m => m.Id == menuId);
            if (!menuExists)
                return NotFound(new { message = "Menu not found" });

            var foodItemExists = await _context.FoodItems.AnyAsync(f => f.Id == dto.FoodItemId);
            if (!foodItemExists)
                return BadRequest(new { message = "Food item not found" });

            var menuItem = new MenuItem
            {
                MenuId = menuId,
                FoodItemId = dto.FoodItemId,
                MealType = dto.MealType,
                ServingSize = dto.ServingSize,
                DisplayOrder = dto.DisplayOrder,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();

            // Update CACFP compliance
            await UpdateMenuCompliance(menuId);

            // Load the food item for response
            await _context.Entry(menuItem).Reference(mi => mi.FoodItem).LoadAsync();

            return CreatedAtAction(nameof(GetMenu), new { id = menuId }, menuItem);
        }

        // PUT: api/Menus/5/items/10
        [HttpPut("{menuId}/items/{itemId}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdateMenuItem(int menuId, int itemId, UpdateMenuItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var menuItem = await _context.MenuItems
                .FirstOrDefaultAsync(mi => mi.Id == itemId && mi.MenuId == menuId);

            if (menuItem == null)
                return NotFound();

            menuItem.FoodItemId = dto.FoodItemId;
            menuItem.MealType = dto.MealType;
            menuItem.ServingSize = dto.ServingSize;
            menuItem.DisplayOrder = dto.DisplayOrder;
            menuItem.Notes = dto.Notes;

            await _context.SaveChangesAsync();

            // Update CACFP compliance
            await UpdateMenuCompliance(menuId);

            return NoContent();
        }

        // DELETE: api/Menus/5/items/10
        [HttpDelete("{menuId}/items/{itemId}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> DeleteMenuItem(int menuId, int itemId)
        {
            var menuItem = await _context.MenuItems
                .FirstOrDefaultAsync(mi => mi.Id == itemId && mi.MenuId == menuId);

            if (menuItem == null)
                return NotFound();

            _context.MenuItems.Remove(menuItem);
            await _context.SaveChangesAsync();

            // Update CACFP compliance
            await UpdateMenuCompliance(menuId);

            return NoContent();
        }

        // PUT: api/Menus/5/items/reorder
        [HttpPut("{menuId}/items/reorder")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> ReorderMenuItems(int menuId, ReorderMenuItemsDto dto)
        {
            var menuItems = await _context.MenuItems
                .Where(mi => mi.MenuId == menuId)
                .ToListAsync();

            foreach (var order in dto.Items)
            {
                var item = menuItems.FirstOrDefault(mi => mi.Id == order.MenuItemId);
                if (item != null)
                {
                    item.DisplayOrder = order.DisplayOrder;
                }
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Menus/5/nutrition
        [HttpGet("{id}/nutrition")]
        public async Task<ActionResult<NutritionSummaryDto>> GetMenuNutrition(int id)
        {
            var menu = await _context.Menus
                .Include(m => m.MenuItems)
                    .ThenInclude(mi => mi.FoodItem)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menu == null)
                return NotFound();

            var summary = CalculateNutritionSummary(menu.MenuItems);
            return Ok(summary);
        }

        private async Task UpdateMenuCompliance(int menuId)
        {
            var menu = await _context.Menus
                .Include(m => m.MenuItems)
                    .ThenInclude(mi => mi.FoodItem)
                .FirstOrDefaultAsync(m => m.Id == menuId);

            if (menu == null) return;

            var categories = menu.MenuItems
                .Where(mi => mi.FoodItem != null)
                .Select(mi => mi.FoodItem!.Category)
                .Distinct()
                .ToList();

            menu.MeetsGrainRequirement = categories.Contains("Grain");
            menu.MeetsProteinRequirement = categories.Contains("Protein");
            menu.MeetsDairyRequirement = categories.Contains("Dairy");
            menu.MeetsFruitVegRequirement = categories.Contains("Fruit") || categories.Contains("Vegetable");
            menu.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private NutritionSummaryDto CalculateNutritionSummary(IEnumerable<MenuItem> items)
        {
            var summary = new NutritionSummaryDto();

            foreach (var item in items.Where(i => i.FoodItem != null))
            {
                summary.TotalCalories += item.FoodItem!.Calories ?? 0;
                summary.TotalProtein += item.FoodItem.Protein ?? 0;
                summary.TotalCarbohydrates += item.FoodItem.Carbohydrates ?? 0;
                summary.TotalFat += item.FoodItem.Fat ?? 0;
                summary.TotalFiber += item.FoodItem.Fiber ?? 0;
                summary.TotalSugar += item.FoodItem.Sugar ?? 0;
            }

            return summary;
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
                CreatedByName = menu.CreatedBy != null
                    ? $"{menu.CreatedBy.FirstName} {menu.CreatedBy.LastName}"
                    : null,
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
                    }).ToList(),
                NutritionSummary = CalculateNutritionSummary(menu.MenuItems)
            };
        }

        private bool MenuExists(int id)
        {
            return _context.Menus.Any(e => e.Id == id);
        }
    }
}
