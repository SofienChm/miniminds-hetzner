using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaycareAPI.Models
{
    public class MenuItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MenuId { get; set; }

        [Required]
        public int FoodItemId { get; set; }

        [Required]
        [StringLength(20)]
        public string MealType { get; set; } = string.Empty; // Breakfast, AM Snack, Lunch, PM Snack, Dinner

        [StringLength(20)]
        public string? ServingSize { get; set; } // e.g., "1/2 cup", "1 slice", "4 oz"

        public int DisplayOrder { get; set; } = 0; // For drag-and-drop ordering

        [StringLength(200)]
        public string? Notes { get; set; } // Special preparation notes

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("MenuId")]
        public Menu? Menu { get; set; }

        [ForeignKey("FoodItemId")]
        public FoodItem? FoodItem { get; set; }
    }
}
