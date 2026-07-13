using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class SlaAuditLogConfiguration : IEntityTypeConfiguration<SlaAuditLog>
{
    public void Configure(EntityTypeBuilder<SlaAuditLog> builder)
    {
        builder.ToTable("SlaAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne(x => x.TicketSla)
            .WithMany()
            .HasForeignKey(x => x.TicketSlaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Ticket)
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TicketSlaId);
        builder.HasIndex(x => x.TicketId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
