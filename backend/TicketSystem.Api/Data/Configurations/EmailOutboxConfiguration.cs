using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> builder)
    {
        builder.ToTable("EmailOutbox");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.RecipientEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.RecipientName)
            .HasMaxLength(200);

        builder.Property(e => e.SenderEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.BodyHtml)
            .IsRequired();

        builder.Property(e => e.InReplyTo)
            .HasMaxLength(255);

        builder.Property(e => e.References)
            .HasMaxLength(4000);

        builder.Property(e => e.TicketMessageId);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Pending");

        builder.Property(e => e.LastError)
            .HasMaxLength(2000);

        builder.Property(e => e.SentMessageId)
            .HasMaxLength(255);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(e => e.Ticket)
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Requester)
            .WithMany()
            .HasForeignKey(e => e.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.NextAttemptAt);
        builder.HasIndex(e => e.CreatedAt);
    }
}
