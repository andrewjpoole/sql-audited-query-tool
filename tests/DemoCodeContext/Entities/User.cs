using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoCodeContext.Entities;

[Table("Users")]
public class User
{
    [Key]
    public int UserID { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Department { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Deposit> ProcessedDeposits { get; set; } = new List<Deposit>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
