using Microsoft.EntityFrameworkCore;
using DemoCodeContext.Entities;

namespace DemoCodeContext.Data;

/// <summary>
/// DbContext for a cash deposit platform that partners with multiple banks
/// to accept customer deposits at various physical locations.
/// </summary>
public class CashDepositDbContext : DbContext
{
    public CashDepositDbContext(DbContextOptions<CashDepositDbContext> options)
        : base(options)
    {
    }

    public DbSet<Partner> Partners { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<DepositLocation> DepositLocations { get; set; } = null!;
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<Deposit> Deposits { get; set; } = null!;
    public DbSet<Fee> Fees { get; set; } = null!;
    public DbSet<Reconciliation> Reconciliations { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Partner configuration
        modelBuilder.Entity<Partner>(entity =>
        {
            entity.HasKey(e => e.PartnerID);
            entity.HasIndex(e => e.PartnerCode).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Status).HasDefaultValue("Active");
            entity.Property(e => e.FeePercentage).HasDefaultValue(0.0050m);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserID);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Role);
            entity.Property(e => e.Status).HasDefaultValue("Active");
        });

        // DepositLocation configuration
        modelBuilder.Entity<DepositLocation>(entity =>
        {
            entity.HasKey(e => e.LocationID);
            entity.HasIndex(e => e.LocationCode).IsUnique();
            entity.HasIndex(e => e.City);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Status).HasDefaultValue("Active");
        });

        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountID);
            entity.HasIndex(e => e.AccountNumber).IsUnique();
            entity.HasIndex(e => e.PartnerID);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Status).HasDefaultValue("Active");
            entity.Property(e => e.Currency).HasDefaultValue("USD");
            entity.Property(e => e.Balance).HasDefaultValue(0m);

            // Relationship to Partner
            entity.HasOne(e => e.Partner)
                .WithMany(p => p.Accounts)
                .HasForeignKey(e => e.PartnerID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Deposit configuration
        modelBuilder.Entity<Deposit>(entity =>
        {
            entity.HasKey(e => e.DepositID);
            entity.HasIndex(e => e.AccountID);
            entity.HasIndex(e => e.LocationID);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ReceivedDate);
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            // Relationships
            entity.HasOne(e => e.Account)
                .WithMany(a => a.Deposits)
                .HasForeignKey(e => e.AccountID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Location)
                .WithMany(l => l.Deposits)
                .HasForeignKey(e => e.LocationID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProcessedByUser)
                .WithMany(u => u.ProcessedDeposits)
                .HasForeignKey(e => e.ProcessedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Fee configuration
        modelBuilder.Entity<Fee>(entity =>
        {
            entity.HasKey(e => e.FeeID);
            entity.HasIndex(e => new { e.PartnerID, e.DepositType });

            entity.HasOne(e => e.Partner)
                .WithMany(p => p.Fees)
                .HasForeignKey(e => e.PartnerID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Reconciliation configuration
        modelBuilder.Entity<Reconciliation>(entity =>
        {
            entity.HasKey(e => e.ReconciliationID);
            entity.HasIndex(e => e.DepositID);
            entity.HasIndex(e => e.ReconciliationDate);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Status).HasDefaultValue("Pending");

            entity.HasOne(e => e.Deposit)
                .WithMany(d => d.Reconciliations)
                .HasForeignKey(e => e.DepositID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ReconciledByUser)
                .WithMany()
                .HasForeignKey(e => e.ReconciledBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditID);
            entity.HasIndex(e => e.TableName);
            entity.HasIndex(e => e.PerformedAt);

            entity.HasOne(e => e.PerformedByUser)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(e => e.PerformedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
