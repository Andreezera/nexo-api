using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Entities;

namespace Nexo.Infrastructure.Persistence.Configurations;

public class SavingsGoalConfiguration : IEntityTypeConfiguration<SavingsGoal>
{
    public void Configure(EntityTypeBuilder<SavingsGoal> builder)
    {
        builder.ToTable("SavingsGoals");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.TargetAmount)
            .HasColumnType("numeric(12,2)");

        builder.Property(g => g.SavedAmount)
            .HasColumnType("numeric(12,2)");

        builder.HasIndex(g => new { g.UserId, g.IsActive });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
