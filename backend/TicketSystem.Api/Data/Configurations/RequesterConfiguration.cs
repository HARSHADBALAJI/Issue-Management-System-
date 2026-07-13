using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class RequesterConfiguration : IEntityTypeConfiguration<Requester>
{
    public void Configure(EntityTypeBuilder<Requester> builder)
    {
        builder.ToTable("Requesters");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.FullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Company)
            .HasMaxLength(100);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(r => r.Email)
            .IsUnique();
    }
}
