using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoCodeContext.Entities;

[Table("Reconciliation")]
public class Reconciliation
{
    [Key]
    public int ReconciliationID { get; set; }

    [Required]
    public int DepositID { get; set; }

    [Required]
    public DateTime ReconciliationDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Variance { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int? ReconciledBy { get; set; }

    public DateTime? ReconciledAt { get; set; }

    // Navigation properties
    [ForeignKey("DepositID")]
    public virtual Deposit Deposit { get; set; } = null!;

    [ForeignKey("ReconciledBy")]
    public virtual User? ReconciledByUser { get; set; }

    // Business logic methods

    /// <summary>
    /// Detects variance between expected and actual amounts.
    /// </summary>
    public bool HasVariance => Variance != 0;

    /// <summary>
    /// Detects unresolved discrepancies - status shows discrepancy but no reconciler assigned.
    /// </summary>
    public bool IsUnresolved => Status == "Discrepancy" && ReconciledBy == null;
}
