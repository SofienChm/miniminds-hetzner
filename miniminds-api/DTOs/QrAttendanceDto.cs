using System.ComponentModel.DataAnnotations;

namespace DaycareAPI.DTOs
{
    public class QrCheckInDto
    {
        [Required]
        public string QrCode { get; set; } = string.Empty;

        [Required]
        public List<int> ChildIds { get; set; } = new List<int>();

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        public string? Notes { get; set; }
    }

    public class QrCheckOutDto
    {
        [Required]
        public string QrCode { get; set; } = string.Empty;

        [Required]
        public List<int> ChildIds { get; set; } = new List<int>();

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        public string? Notes { get; set; }
    }

    public class QrValidationResponseDto
    {
        public bool IsValid { get; set; }
        public string Type { get; set; } = string.Empty; // "CheckIn" or "CheckOut"
        public string? Message { get; set; }
    }

    public class QrCodeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SchoolSettingsDto
    {
        public int Id { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int GeofenceRadiusMeters { get; set; }
        public bool GeofenceEnabled { get; set; }
    }

    public class UpdateSchoolSettingsDto
    {
        public string? SchoolName { get; set; }

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        [Range(10, 1000)]
        public int GeofenceRadiusMeters { get; set; } = 100;

        public bool GeofenceEnabled { get; set; } = true;
    }

    public class QrAttendanceResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<AttendanceResultItem> Results { get; set; } = new List<AttendanceResultItem>();
    }

    public class AttendanceResultItem
    {
        public int ChildId { get; set; }
        public string ChildName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? AttendanceId { get; set; }
    }
}
