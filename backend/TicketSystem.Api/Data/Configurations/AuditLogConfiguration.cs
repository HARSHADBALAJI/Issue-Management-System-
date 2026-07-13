using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(al => al.Id);

        builder.Property(al => al.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(al => al.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(al => al.OldValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(al => al.NewValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(al => al.IpAddress)
            .HasMaxLength(50);

        builder.Property(al => al.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(al => al.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(al => al.Requester)
            .WithMany(r => r.AuditLogs)
            .HasForeignKey(al => al.RequesterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(al => al.CreatedAt);
        builder.HasIndex(al => al.EntityType);
        builder.HasIndex(al => al.EntityId);
    }
}
