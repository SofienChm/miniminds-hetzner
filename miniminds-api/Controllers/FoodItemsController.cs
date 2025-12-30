using DaycareAPI.Data;
using DaycareAPI.Models;
using DaycareAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FoodItemsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FoodItemsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/FoodItems
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FoodItem>>> GetFoodItems(
            [FromQuery] string? category = null,
            [FromQuery] bool? activeOnly = true,
            [FromQuery] string? search = null)
        {
            var query = _context.FoodItems.AsQueryable();

            if (activeOnly == true)
            {
                query = query.Where(f => f.IsActive);
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(f => f.Category == category);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(f => f.Name.Contains(search) ||
                                        (f.Description != null && f.Description.Contains(search)));
            }

            return await query.OrderBy(f => f.Name).ToListAsync();
        }

        // GET: api/FoodItems/5
        [HttpGet("{id}")]
        public async Task<ActionResult<FoodItem>> GetFoodItem(int id)
        {
            var foodItem = await _context.FoodItems.FindAsync(id);

            if (foodItem == null)
                return NotFound();

            return foodItem;
        }

        // GET: api/FoodItems/Categories
        [HttpGet("Categories")]
        public ActionResult<IEnumerable<string>> GetCategories()
        {
            var categories = new List<string>
            {
                "Grain",
                "Protein",
                "Dairy",
                "Fruit",
                "Vegetable",
                "Beverage",
                "Other"
            };
            return Ok(categories);
        }

        // GET: api/FoodItems/Allergens
        [HttpGet("Allergens")]
        public ActionResult<IEnumerable<string>> GetAllergens()
        {
            var allergens = new List<string>
            {
                "Milk",
                "Eggs",
                "Peanuts",
                "Tree Nuts",
                "Wheat",
                "Soy",
                "Fish",
                "Shellfish",
                "Sesame"
            };
            return Ok(allergens);
        }

        // POST: api/FoodItems
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<FoodItem>> CreateFoodItem(CreateFoodItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var foodItem = new FoodItem
            {
                Name = dto.Name,
                Description = dto.Description,
                Category = dto.Category,
                Calories = dto.Calories,
                Protein = dto.Protein,
                Carbohydrates = dto.Carbohydrates,
                Fat = dto.Fat,
                Fiber = dto.Fiber,
                Sugar = dto.Sugar,
                Allergens = dto.Allergens,
                DietaryTags = dto.DietaryTags,
                ImageUrl = dto.ImageUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.FoodItems.Add(foodItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetFoodItem), new { id = foodItem.Id }, foodItem);
        }

        // PUT: api/FoodItems/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdateFoodItem(int id, UpdateFoodItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var foodItem = await _context.FoodItems.FindAsync(id);
            if (foodItem == null)
                return NotFound();

            foodItem.Name = dto.Name;
            foodItem.Description = dto.Description;
            foodItem.Category = dto.Category;
            foodItem.Calories = dto.Calories;
            foodItem.Protein = dto.Protein;
            foodItem.Carbohydrates = dto.Carbohydrates;
            foodItem.Fat = dto.Fat;
            foodItem.Fiber = dto.Fiber;
            foodItem.Sugar = dto.Sugar;
            foodItem.Allergens = dto.Allergens;
            foodItem.DietaryTags = dto.DietaryTags;
            foodItem.ImageUrl = dto.ImageUrl;
            foodItem.IsActive = dto.IsActive;
            foodItem.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FoodItemExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/FoodItems/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFoodItem(int id)
        {
            var foodItem = await _context.FoodItems.FindAsync(id);
            if (foodItem == null)
                return NotFound();

            // Check if food item is used in any menu
            var isUsedInMenu = await _context.MenuItems.AnyAsync(mi => mi.FoodItemId == id);
            if (isUsedInMenu)
            {
                // Soft delete - just mark as inactive
                foodItem.IsActive = false;
                foodItem.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Food item deactivated (used in existing menus)" });
            }

            _context.FoodItems.Remove(foodItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/FoodItems/5/toggle-status
        [HttpPut("{id}/toggle-status")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var foodItem = await _context.FoodItems.FindAsync(id);
            if (foodItem == null)
                return NotFound();

            foodItem.IsActive = !foodItem.IsActive;
            foodItem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { isActive = foodItem.IsActive });
        }

        private bool FoodItemExists(int id)
        {
            return _context.FoodItems.Any(e => e.Id == id);
        }
    }
}
