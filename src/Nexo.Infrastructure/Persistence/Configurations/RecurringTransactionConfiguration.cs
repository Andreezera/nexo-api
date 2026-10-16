using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexo.Domain.Entities;

namespace Nexo.Infrastructure.Persistence.Configurations;

public class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
    {
        builder.ToTable("RecurringTransactions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Amount)
            .HasColumnType("numeric(12,2)");

        builder.Property(r => r.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(r => new { r.UserId, r.IsActive });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
