using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Entities;

namespace Nexo.Infrastructure.Persistence.Configurations;

public class BudgetLimitConfiguration : IEntityTypeConfiguration<BudgetLimit>
{
    public void Configure(EntityTypeBuilder<BudgetLimit> builder)
    {
        builder.ToTable("BudgetLimits");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(b => b.MonthlyLimit)
            .HasColumnType("numeric(12,2)");

        // um limite por categoria para cada usuário
        builder.HasIndex(b => new { b.UserId, b.Category })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
