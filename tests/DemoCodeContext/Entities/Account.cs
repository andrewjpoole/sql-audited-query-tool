using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoCodeContext.Entities;

[Table("Accounts")]
public class Account
{
    [Key]
    public int AccountID { get; set; }

    [Required]
    public int PartnerID { get; set; }

    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string HolderName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0m;

    [Required]
    [MaxLength(30)]
    public string KYCStatus { get; set; } = "Pending";

    public DateTime? KYCVerifiedDate { get; set; }

    public DateTime OpenedDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("PartnerID")]
    public virtual Partner Partner { get; set; } = null!;

    public virtual ICollection<Deposit> Deposits { get; set; } = new List<Deposit>();
}
