using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoCodeContext.Entities;

[Table("Fees")]
public class Fee
{
    [Key]
    public int FeeID { get; set; }

    [Required]
    public int PartnerID { get; set; }

    [Required]
    [MaxLength(30)]
    public string DepositType { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,4)")]
    public decimal Percentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxFee { get; set; }

    public DateTime EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    // Navigation properties
    [ForeignKey("PartnerID")]
    public virtual Partner Partner { get; set; } = null!;
}
