using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaycareAPI.Models
{
    public class Menu
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty; // e.g., "Week 1 December Menu", "Monday Menu"

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime MenuDate { get; set; } // The date this menu is for

        [Required]
        [StringLength(20)]
        public string MenuType { get; set; } = "Daily"; // Daily, Weekly

        public bool IsPublished { get; set; } = false; // Only published menus are visible to parents

        public bool IsTemplate { get; set; } = false; // Templates can be duplicated for future weeks

        [StringLength(500)]
        public string? Notes { get; set; } // Special notes for the day/week

        // CACFP Compliance fields
        public bool MeetsGrainRequirement { get; set; } = false;
        public bool MeetsProteinRequirement { get; set; } = false;
        public bool MeetsDairyRequirement { get; set; } = false;
        public bool MeetsFruitVegRequirement { get; set; } = false;

        public int? CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("CreatedById")]
        public Teacher? CreatedBy { get; set; }

        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}
