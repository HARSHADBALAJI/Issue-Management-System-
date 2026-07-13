using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");

        builder.HasKey(tm => tm.Id);

        builder.Property(tm => tm.MessageSourceType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(tm => tm.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(tm => tm.IsInternal)
            .HasDefaultValue(false);

        builder.Property(tm => tm.InReplyTo)
            .HasMaxLength(255);

        builder.Property(tm => tm.References)
            .HasMaxLength(1000);

        builder.Property(tm => tm.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(tm => tm.Ticket)
            .WithMany(t => t.Messages)
            .HasForeignKey(tm => tm.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tm => tm.Requester)
            .WithMany(r => r.Messages)
            .HasForeignKey(tm => tm.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tm => tm.User)
            .WithMany(u => u.Messages)
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tm => tm.TicketId);
        builder.HasIndex(tm => tm.CreatedAt);

        builder.ToTable(tb => tb.HasCheckConstraint("CK_TicketMessage_SourceType", @"
            (RequesterId IS NOT NULL AND UserId IS NULL AND MessageSourceType = 'Requester') OR
            (RequesterId IS NULL AND UserId IS NOT NULL AND MessageSourceType IN ('User', 'System'))
        "));
    }
}
