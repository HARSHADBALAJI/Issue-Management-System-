using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachments");

        builder.HasKey(ta => ta.Id);

        builder.Property(ta => ta.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(ta => ta.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ta => ta.FileSize)
            .IsRequired();

        builder.Property(ta => ta.FileData)
            .IsRequired()
            .HasColumnType("varbinary(max)");

        builder.Property(ta => ta.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(ta => ta.TicketMessage)
            .WithMany(tm => tm.Attachments)
            .HasForeignKey(ta => ta.TicketMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ta => ta.TicketMessageId);
    }
}
