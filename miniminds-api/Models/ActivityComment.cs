using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaycareAPI.Models
{
    public class ActivityComment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ActivityId { get; set; }

        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        // User who posted the comment
        [Required]
        public string UserId { get; set; } = string.Empty;

        // Parent comment ID for replies
        public int? ParentCommentId { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Soft delete
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        [ForeignKey("ActivityId")]
        public DailyActivity? Activity { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [ForeignKey("ParentCommentId")]
        public ActivityComment? ParentComment { get; set; }

        public ICollection<ActivityComment> Replies { get; set; } = new List<ActivityComment>();
    }
}
