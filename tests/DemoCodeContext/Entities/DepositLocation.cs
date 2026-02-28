using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoCodeContext.Entities;

[Table("DepositLocations")]
public class DepositLocation
{
    [Key]
    public int LocationID { get; set; }

    [Required]
    [MaxLength(20)]
    public string LocationCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string LocationType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string LocationName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string State { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    [MaxLength(100)]
    public string? OpeningHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxDepositAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Deposit> Deposits { get; set; } = new List<Deposit>();
}
