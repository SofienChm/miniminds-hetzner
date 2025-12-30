using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaycareAPI.Models
{
    public class Photo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        // Keep FilePath for backward compatibility (can be null for new photos stored in DB)
        [StringLength(500)]
        public string? FilePath { get; set; }

        [StringLength(50)]
        public string FileType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        // Image data stored as Base64 in database (LONGTEXT in MySQL)
        // Full resolution image - loaded only when viewing single photo
        [Column(TypeName = "LONGTEXT")]
        public string? ImageData { get; set; }

        // Thumbnail for gallery view - small base64 image (~20KB)
        // This makes gallery loading fast without loading full images
        [Column(TypeName = "MEDIUMTEXT")]
        public string? ThumbnailData { get; set; }

        [StringLength(500)]
        public string? Title { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        // Photo category: Memory, Activity, Event, General
        [StringLength(50)]
        public string Category { get; set; } = "Memory";

        // Related entity (optional - for linking to activities or events)
        [StringLength(50)]
        public string? RelatedEntityType { get; set; }

        public int? RelatedEntityId { get; set; }

        // Direct link to activity (optional)
        public int? ActivityId { get; set; }

        [ForeignKey("ActivityId")]
        public DailyActivity? Activity { get; set; }

        // Child relationship (required - photos must be associated with a child)
        [Required]
        public int ChildId { get; set; }

        [ForeignKey("ChildId")]
        public Child? Child { get; set; }

        // Uploaded by user
        [StringLength(255)]
        public string? UploadedById { get; set; }

        [ForeignKey("UploadedById")]
        public ApplicationUser? UploadedBy { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
