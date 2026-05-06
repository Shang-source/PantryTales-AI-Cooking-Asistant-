using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class NameNormalizationDictionaryVersionConfiguration : IEntityTypeConfiguration<NameNormalizationDictionaryVersion>
{
    public void Configure(EntityTypeBuilder<NameNormalizationDictionaryVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.ToTable(t => t.HasCheckConstraint("CK_name_normalization_dictionary_versions_singleton", "id = 1"));

        builder.Property(v => v.DictionaryVersion)
            .HasDefaultValue(1);

        builder.Property(v => v.AlgorithmVersion)
            .HasDefaultValue(1);

        builder.Property(v => v.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");
    }
}
