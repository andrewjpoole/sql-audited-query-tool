using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoCodeContext.Entities;

[Table("AuditLog")]
public class AuditLog
{
    [Key]
    public int AuditID { get; set; }

    [Required]
    [MaxLength(50)]
    public string TableName { get; set; } = string.Empty;

    [Required]
    public int RecordID { get; set; }

    [Required]
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    public int? PerformedBy { get; set; }

    [Required]
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "nvarchar(max)")]
    public string? OldValues { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? NewValues { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("PerformedBy")]
    public virtual User? PerformedByUser { get; set; }
}
