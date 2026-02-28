using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoCodeContext.Entities;

[Table("Partners")]
public class Partner
{
    [Key]
    public int PartnerID { get; set; }

    [Required]
    [MaxLength(20)]
    public string PartnerCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PartnerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    [Required]
    public DateTime OnboardedDate { get; set; }

    [MaxLength(100)]
    public string? ApiKey { get; set; }

    [MaxLength(50)]
    public string? SettlementAccountNumber { get; set; }

    [MaxLength(100)]
    public string? ContactEmail { get; set; }

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal FeePercentage { get; set; } = 0.0050m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DailyDepositLimit { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
    public virtual ICollection<Fee> Fees { get; set; } = new List<Fee>();
}
