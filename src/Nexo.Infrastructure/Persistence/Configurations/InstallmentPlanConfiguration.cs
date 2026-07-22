using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Entities;

namespace Nexo.Infrastructure.Persistence.Configurations;

public class InstallmentPlanConfiguration : IEntityTypeConfiguration<InstallmentPlan>
{
    public void Configure(EntityTypeBuilder<InstallmentPlan> builder)
    {
        builder.ToTable("InstallmentPlans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.TotalAmount)
            .HasColumnType("numeric(12,2)");

        builder.HasMany(p => p.Transactions)
            .WithOne(t => t.InstallmentPlan)
            .HasForeignKey(t => t.InstallmentPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Transactions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
