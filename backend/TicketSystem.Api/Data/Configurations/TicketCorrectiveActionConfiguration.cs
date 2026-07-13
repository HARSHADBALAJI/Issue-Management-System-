using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class TicketCorrectiveActionConfiguration : IEntityTypeConfiguration<TicketCorrectiveAction>
{
    public void Configure(EntityTypeBuilder<TicketCorrectiveAction> builder)
    {
        builder.ToTable("TicketCorrectiveActions");

        builder.HasKey(tca => tca.Id);

        builder.Property(tca => tca.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(tca => tca.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(tca => tca.Ticket)
            .WithMany(t => t.CorrectiveActions)
            .HasForeignKey(tca => tca.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tca => tca.PerformedByUser)
            .WithMany(u => u.CorrectiveActions)
            .HasForeignKey(tca => tca.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tca => tca.TicketId);
    }
}
