using BudgetManagementBotSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetManagementBotSystem.InfraStructure.Persistence;

public class BudgetManagementDbContext : DbContext
{
    public DbSet<Group> Groups { get; set; }
    public DbSet<BudgetRequest> BudgetRequests { get; set; }
    public DbSet<RequestStatusChange> RequestStatusChanges { get; set; }

    public BudgetManagementDbContext(DbContextOptions<BudgetManagementDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entity relationships and constraints here if needed
    }
}
