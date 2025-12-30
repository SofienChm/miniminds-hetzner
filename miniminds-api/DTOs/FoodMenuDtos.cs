using System.ComponentModel.DataAnnotations;

namespace DaycareAPI.DTOs
{
    // Food Item DTOs
    public class CreateFoodItemDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        public int? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Carbohydrates { get; set; }
        public decimal? Fat { get; set; }
        public decimal? Fiber { get; set; }
        public decimal? Sugar { get; set; }

        [StringLength(500)]
        public string? Allergens { get; set; }

        [StringLength(200)]
        public string? DietaryTags { get; set; }

        public string? ImageUrl { get; set; }
    }

    public class UpdateFoodItemDto : CreateFoodItemDto
    {
        public bool IsActive { get; set; } = true;
    }

    // Menu DTOs
    public class CreateMenuDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime MenuDate { get; set; }

        [Required]
        [StringLength(20)]
        public string MenuType { get; set; } = "Daily";

        public bool IsTemplate { get; set; } = false;

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class UpdateMenuDto : CreateMenuDto
    {
        public bool IsPublished { get; set; } = false;
        public bool MeetsGrainRequirement { get; set; } = false;
        public bool MeetsProteinRequirement { get; set; } = false;
        public bool MeetsDairyRequirement { get; set; } = false;
        public bool MeetsFruitVegRequirement { get; set; } = false;
    }

    public class DuplicateMenuDto
    {
        [Required]
        public int SourceMenuId { get; set; }

        [Required]
        public DateTime NewMenuDate { get; set; }

        [StringLength(100)]
        public string? NewName { get; set; }
    }

    // Menu Item DTOs
    public class CreateMenuItemDto
    {
        [Required]
        public int MenuId { get; set; }

        [Required]
        public int FoodItemId { get; set; }

        [Required]
        [StringLength(20)]
        public string MealType { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ServingSize { get; set; }

        public int DisplayOrder { get; set; } = 0;

        [StringLength(200)]
        public string? Notes { get; set; }
    }

    public class UpdateMenuItemDto
    {
        [Required]
        public int FoodItemId { get; set; }

        [Required]
        [StringLength(20)]
        public string MealType { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ServingSize { get; set; }

        public int DisplayOrder { get; set; } = 0;

        [StringLength(200)]
        public string? Notes { get; set; }
    }

    public class ReorderMenuItemsDto
    {
        [Required]
        public List<MenuItemOrderDto> Items { get; set; } = new();
    }

    public class MenuItemOrderDto
    {
        public int MenuItemId { get; set; }
        public int DisplayOrder { get; set; }
    }

    // Menu Selection DTOs
    public class CreateMenuSelectionDto
    {
        [Required]
        public int ChildId { get; set; }

        [Required]
        public int MenuId { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        public bool IsSelected { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class UpdateMenuSelectionDto
    {
        public bool IsSelected { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(50)]
        public string SelectionStatus { get; set; } = "Pending";
    }

    public class BulkMenuSelectionDto
    {
        [Required]
        public int ChildId { get; set; }

        [Required]
        public int MenuId { get; set; }

        [Required]
        public List<MenuItemSelectionDto> Selections { get; set; } = new();
    }

    public class MenuItemSelectionDto
    {
        public int MenuItemId { get; set; }
        public bool IsSelected { get; set; } = true;
        public string? Notes { get; set; }
    }

    // Response DTOs
    public class MenuWithItemsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime MenuDate { get; set; }
        public string MenuType { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
        public bool IsTemplate { get; set; }
        public string? Notes { get; set; }
        public bool MeetsGrainRequirement { get; set; }
        public bool MeetsProteinRequirement { get; set; }
        public bool MeetsDairyRequirement { get; set; }
        public bool MeetsFruitVegRequirement { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<MenuItemWithFoodDto> MenuItems { get; set; } = new();
        public NutritionSummaryDto? NutritionSummary { get; set; }
    }

    public class MenuItemWithFoodDto
    {
        public int Id { get; set; }
        public string MealType { get; set; } = string.Empty;
        public string? ServingSize { get; set; }
        public int DisplayOrder { get; set; }
        public string? Notes { get; set; }
        public FoodItemDto FoodItem { get; set; } = new();
    }

    public class FoodItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public int? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Carbohydrates { get; set; }
        public decimal? Fat { get; set; }
        public decimal? Fiber { get; set; }
        public decimal? Sugar { get; set; }
        public string? Allergens { get; set; }
        public string? DietaryTags { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
    }

    public class NutritionSummaryDto
    {
        public int TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public decimal TotalCarbohydrates { get; set; }
        public decimal TotalFat { get; set; }
        public decimal TotalFiber { get; set; }
        public decimal TotalSugar { get; set; }
    }

    public class ChildMenuViewDto
    {
        public int ChildId { get; set; }
        public string ChildName { get; set; } = string.Empty;
        public string? Allergies { get; set; }
        public MenuWithItemsDto Menu { get; set; } = new();
        public List<MenuSelectionDto> Selections { get; set; } = new();
        public List<string> AllergyWarnings { get; set; } = new();
    }

    public class MenuSelectionDto
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public bool IsSelected { get; set; }
        public string? Notes { get; set; }
        public string SelectionStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
