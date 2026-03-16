using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BudgetManagementBotSystem.InfraStructure.Persistence;

public class BudgetManagementDbContext : DbContext
{
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BudgetRequest> BudgetRequests => Set<BudgetRequest>();
    public DbSet<BudgetTransaction> BudgetTransactions => Set<BudgetTransaction>();
    public DbSet<RequestEvidence> RequestEvidences => Set<RequestEvidence>();
    public DbSet<RequestStatusChange> RequestStatusChanges => Set<RequestStatusChange>();

    public BudgetManagementDbContext(DbContextOptions<BudgetManagementDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var moneyConverter = new ValueConverter<Money, decimal>(
            v => v.Value,
            v => new Money(v));

        var fiscalYearConverter = new ValueConverter<FiscalYear, int>(
            v => v.Year,
            v => new FiscalYear(v)); // FiscalYear側に year 復元用ctorが必要

        modelBuilder.Entity<Group>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.IsActive).IsRequired();
            e.HasIndex(x => x.DiscordUserId).IsUnique();
            e.HasOne<Group>()
             .WithMany()
             .HasForeignKey(x => x.GroupId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetTransaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasConversion(moneyConverter).HasPrecision(18, 2).IsRequired();
            e.Property(x => x.FiscalYear).HasConversion(fiscalYearConverter).IsRequired();
            e.Property(x => x.TransactionDate).IsRequired();
            e.Property(x => x.IsIncome).IsRequired();

            e.HasOne<Group>()
             .WithMany(g => g.BudgetTransactions)
             .HasForeignKey("GroupId")
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BudgetRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasConversion(moneyConverter).HasPrecision(18, 2).IsRequired();
            e.Property(x => x.FiscalYear).HasConversion(fiscalYearConverter).IsRequired();
            e.Property(x => x.RequestDate).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();

            e.HasOne<Group>()
             .WithMany(g => g.Requests)
             .HasForeignKey("GroupId")
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<User>()
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RequestEvidence>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FilePath).HasMaxLength(2048).IsRequired();

            e.HasOne<BudgetRequest>()
             .WithMany(r => r.Evidences)
             .HasForeignKey("BudgetRequestId")
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RequestStatusChange>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ChangedStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.ChangedAt).IsRequired();

            e.HasOne<BudgetRequest>()
             .WithMany(r => r.StatusHistory)
             .HasForeignKey("BudgetRequestId")
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex("BudgetRequestId", nameof(RequestStatusChange.ChangedAt));
        });
    }
}
