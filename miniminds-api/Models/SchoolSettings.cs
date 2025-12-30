using System.ComponentModel.DataAnnotations;

namespace DaycareAPI.Models
{
    public class SchoolSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string SchoolName { get; set; } = "MiniMinds Daycare";

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        [Required]
        public int GeofenceRadiusMeters { get; set; } = 100;

        public bool GeofenceEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
