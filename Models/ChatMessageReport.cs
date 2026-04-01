using BookSharingApp.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookSharingApp.Models
{
    [Table("chat_message_report")]
    public class ChatMessageReport
    {
        [Key]
        [Column("report_id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("message_id")]
        public int MessageId { get; set; }

        [Required]
        [Column("reporter_id")]
        public string ReporterId { get; set; } = string.Empty;

        [Required]
        [Column("reported_user_id")]
        public string ReportedUserId { get; set; } = string.Empty;

        [Required]
        [Column("category")]
        public ReportCategory Category { get; set; }

        [Required]
        [Column("reported_content")]
        [MaxLength(2000)]
        public string ReportedContent { get; set; } = string.Empty;

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ChatMessage Message { get; set; } = null!;
        public User Reporter { get; set; } = null!;
        public User ReportedUser { get; set; } = null!;
    }
}
