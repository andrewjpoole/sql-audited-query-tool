using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoCodeContext.Entities;

[Table("Deposits")]
public class Deposit
{
    [Key]
    public int DepositID { get; set; }

    [Required]
    public int AccountID { get; set; }

    [Required]
    public int LocationID { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(30)]
    public string DepositType { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime ReceivedDate { get; set; }

    public DateTime? ProcessedDate { get; set; }

    public DateTime? SettledDate { get; set; }

    public int? ProcessedBy { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FeeAmount { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetAmount { get; set; } = 0m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("AccountID")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("LocationID")]
    public virtual DepositLocation Location { get; set; } = null!;

    [ForeignKey("ProcessedBy")]
    public virtual User? ProcessedByUser { get; set; }

    public virtual ICollection<Reconciliation> Reconciliations { get; set; } = new List<Reconciliation>();
}
