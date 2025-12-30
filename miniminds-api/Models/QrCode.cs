using System.ComponentModel.DataAnnotations;

namespace DaycareAPI.Models
{
    public class QrCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Type { get; set; } = string.Empty; // "CheckIn" or "CheckOut"

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
