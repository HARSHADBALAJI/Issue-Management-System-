using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class ApplicationRoutingRuleConfiguration : IEntityTypeConfiguration<ApplicationRoutingRule>
{
    public void Configure(EntityTypeBuilder<ApplicationRoutingRule> builder)
    {
        builder.ToTable("ApplicationRoutingRules");

        builder.HasKey(arr => arr.Id);

        builder.Property(arr => arr.IsActive)
            .HasDefaultValue(true);

        builder.Property(arr => arr.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(arr => arr.Application)
            .WithMany(a => a.RoutingRules)
            .HasForeignKey(arr => arr.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(arr => arr.Department)
            .WithMany()
            .HasForeignKey(arr => arr.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(arr => arr.PrimarySpocUser)
            .WithMany()
            .HasForeignKey(arr => arr.PrimarySpocUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(arr => arr.BackupSpocUser)
            .WithMany()
            .HasForeignKey(arr => arr.BackupSpocUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(arr => new { arr.ApplicationId, arr.DepartmentId })
            .IsUnique();

        builder.HasQueryFilter(arr => arr.IsActive);
    }
}
