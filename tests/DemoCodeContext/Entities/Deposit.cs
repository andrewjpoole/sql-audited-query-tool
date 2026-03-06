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

    // Business logic methods

    /// <summary>
    /// Detects potential structuring risk - deposits just under the $10,000 reporting threshold.
    /// Seed data: deposits 150-153 have this pattern.
    /// </summary>
    public bool IsStructuringRisk => Amount >= 9000m && Amount <= 9999m;

    /// <summary>
    /// Detects ghost deposits - completed without a processing user.
    /// Seed data: deposits 180-182 have this anomaly.
    /// </summary>
    public bool IsGhostDeposit => Status == "Completed" && ProcessedBy == null;

    /// <summary>
    /// Detects settlement delays - completed and processed but not settled within 3 business days.
    /// </summary>
    public bool IsSettlementOverdue
    {
        get
        {
            if (Status != "Completed" || !ProcessedDate.HasValue || SettledDate.HasValue)
                return false;

            var businessDays = 0;
            var current = ProcessedDate.Value.Date;
            var today = DateTime.UtcNow.Date;

            while (current < today)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                    businessDays++;
                current = current.AddDays(1);
            }

            return businessDays > 3;
        }
    }

    /// <summary>
    /// Calculates fee amount based on percentage. Returns negative fees for negative percentages (anomaly for partner PSB).
    /// </summary>
    /// <param name="feePercentage">Fee percentage (e.g., 0.0050 for 0.5%)</param>
    /// <returns>Calculated fee amount</returns>
    public decimal CalculateFee(decimal feePercentage) => Amount * feePercentage;
}
