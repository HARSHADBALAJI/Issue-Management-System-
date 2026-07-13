using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.Subject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("medium");

        builder.Property(t => t.StatusId)
            .HasDefaultValue(5);

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(t => t.Requester)
            .WithMany(r => r.Tickets)
            .HasForeignKey(t => t.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Application)
            .WithMany(a => a.Tickets)
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedToUser)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Status)
            .WithMany(s => s.Tickets)
            .HasForeignKey(t => t.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.TicketNumber)
            .IsUnique();

        builder.HasIndex(t => t.StatusId);
        builder.HasIndex(t => t.AssignedToUserId);
        builder.HasIndex(t => t.RequesterId);
        builder.HasIndex(t => t.ApplicationId);
        builder.HasIndex(t => t.CreatedAt);
    }
}
