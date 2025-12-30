using System.ComponentModel.DataAnnotations;

namespace DaycareAPI.Models
{
    public class FoodItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty; // Grain, Protein, Dairy, Fruit, Vegetable, Beverage, Other

        // Nutritional Information
        public int? Calories { get; set; }
        public decimal? Protein { get; set; } // in grams
        public decimal? Carbohydrates { get; set; } // in grams
        public decimal? Fat { get; set; } // in grams
        public decimal? Fiber { get; set; } // in grams
        public decimal? Sugar { get; set; } // in grams

        [StringLength(500)]
        public string? Allergens { get; set; } // Comma-separated: Milk, Eggs, Peanuts, Tree Nuts, Wheat, Soy, Fish, Shellfish

        [StringLength(200)]
        public string? DietaryTags { get; set; } // Comma-separated: Vegetarian, Vegan, Gluten-Free, Dairy-Free, Nut-Free

        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}
