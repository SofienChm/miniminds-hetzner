namespace DaycareAPI.DTOs
{
    // Lightweight DTO for parent list (without profile picture)
    public class ParentListDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? EmergencyContact { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Work { get; set; }
        public string? ZipCode { get; set; }
        public string? ParentType { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool HasProfilePicture { get; set; }
        public List<ChildListDto> Children { get; set; } = new List<ChildListDto>();
    }

    // Lightweight DTO for child list (without profile picture)
    public class ChildListDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? Allergies { get; set; }
        public string? MedicalNotes { get; set; }
        public int ParentId { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool HasProfilePicture { get; set; }
        public ParentBasicDto? Parent { get; set; }
    }

    // Basic parent info for child list
    public class ParentBasicDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
