using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class TicketStatusHistoryConfiguration : IEntityTypeConfiguration<TicketStatusHistory>
{
    public void Configure(EntityTypeBuilder<TicketStatusHistory> builder)
    {
        builder.ToTable("TicketStatusHistory");

        builder.HasKey(tsh => tsh.Id);

        builder.Property(tsh => tsh.Remarks)
            .HasMaxLength(500);

        builder.Property(tsh => tsh.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(tsh => tsh.Ticket)
            .WithMany(t => t.StatusHistory)
            .HasForeignKey(tsh => tsh.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tsh => tsh.FromStatus)
            .WithMany(s => s.StatusHistoriesAsFrom)
            .HasForeignKey(tsh => tsh.FromStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tsh => tsh.ToStatus)
            .WithMany(s => s.StatusHistoriesAsTo)
            .HasForeignKey(tsh => tsh.ToStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tsh => tsh.ChangedByUser)
            .WithMany(u => u.StatusHistories)
            .HasForeignKey(tsh => tsh.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tsh => tsh.ChangedByRequester)
            .WithMany(r => r.StatusHistories)
            .HasForeignKey(tsh => tsh.ChangedByRequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tsh => tsh.TicketId);
        builder.HasIndex(tsh => tsh.CreatedAt);
    }
}
