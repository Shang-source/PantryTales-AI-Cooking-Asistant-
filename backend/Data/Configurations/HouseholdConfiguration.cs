using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.HasOne(h => h.Owner)
            .WithMany(u => u.OwnedHouseholds)
            .HasForeignKey(h => h.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(h => h.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");
    }
}
