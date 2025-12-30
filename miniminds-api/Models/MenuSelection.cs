using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaycareAPI.Models
{
    public class MenuSelection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ChildId { get; set; }

        [Required]
        public int MenuId { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [Required]
        public int ParentId { get; set; }

        public bool IsSelected { get; set; } = true; // true = child will eat this, false = child will skip

        [StringLength(500)]
        public string? Notes { get; set; } // Parent notes, e.g., "Please give extra portion" or "Allergic, skip this"

        [StringLength(50)]
        public string SelectionStatus { get; set; } = "Pending"; // Pending, Confirmed, Served

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("ChildId")]
        public Child? Child { get; set; }

        [ForeignKey("MenuId")]
        public Menu? Menu { get; set; }

        [ForeignKey("MenuItemId")]
        public MenuItem? MenuItem { get; set; }

        [ForeignKey("ParentId")]
        public Parent? Parent { get; set; }
    }
}
