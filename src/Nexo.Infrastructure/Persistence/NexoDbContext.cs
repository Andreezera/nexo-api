using Microsoft.EntityFrameworkCore;
using Nexo.Domain.Entities;

namespace Nexo.Infrastructure.Persistence;

public class NexoDbContext : DbContext
{
    public NexoDbContext(DbContextOptions<NexoDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
    public DbSet<BudgetLimit> BudgetLimits => Set<BudgetLimit>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NexoDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
