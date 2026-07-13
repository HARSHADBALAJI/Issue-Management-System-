using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable("EmailMessages");

        builder.HasKey(em => em.Id);

        builder.Property(em => em.MessageId)
            .HasMaxLength(255);

        builder.Property(em => em.InReplyTo)
            .HasMaxLength(255);

        builder.Property(em => em.References)
            .HasMaxLength(4000);

        builder.Property(em => em.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(em => em.SenderEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(em => em.RecipientEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(em => em.Direction)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(em => em.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(em => em.ReceivedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(em => em.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(em => em.Ticket)
            .WithMany(t => t.EmailMessages)
            .HasForeignKey(em => em.TicketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(em => em.Requester)
            .WithMany(r => r.EmailMessages)
            .HasForeignKey(em => em.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(em => em.User)
            .WithMany(u => u.EmailMessages)
            .HasForeignKey(em => em.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(em => em.MessageId)
            .IsUnique()
            .HasFilter("[MessageId] IS NOT NULL");

        builder.HasIndex(em => em.TicketId);
        builder.HasIndex(em => em.CreatedAt);
    }
}
