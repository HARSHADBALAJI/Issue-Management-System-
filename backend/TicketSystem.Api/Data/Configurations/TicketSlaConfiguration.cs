using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class TicketSlaConfiguration : IEntityTypeConfiguration<TicketSla>
{
    public void Configure(EntityTypeBuilder<TicketSla> builder)
    {
        builder.ToTable("TicketSlas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Priority).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.TotalPausedDuration).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.DeadlineAt).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne(x => x.Ticket)
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SlaPolicy)
            .WithMany()
            .HasForeignKey(x => x.SlaPolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TicketId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IsActive);
    }
}
