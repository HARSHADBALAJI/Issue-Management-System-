using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class TicketStatusConfiguration : IEntityTypeConfiguration<TicketStatus>
{
    public void Configure(EntityTypeBuilder<TicketStatus> builder)
    {
        builder.ToTable("TicketStatuses");

        builder.HasKey(ts => ts.Id);

        builder.Property(ts => ts.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ts => ts.DisplayName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ts => ts.Color)
            .HasMaxLength(20);

        builder.Property(ts => ts.IsActive)
            .HasDefaultValue(true);

        builder.Property(ts => ts.SortOrder)
            .HasDefaultValue(0);

        builder.Property(ts => ts.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(ts => ts.Name)
            .IsUnique();

        builder.HasQueryFilter(ts => ts.IsActive);
    }
}
