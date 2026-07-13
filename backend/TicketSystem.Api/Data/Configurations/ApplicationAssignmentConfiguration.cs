using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class ApplicationAssignmentConfiguration : IEntityTypeConfiguration<ApplicationAssignment>
{
    public void Configure(EntityTypeBuilder<ApplicationAssignment> builder)
    {
        builder.ToTable("ApplicationAssignments");

        builder.HasKey(aa => aa.Id);

        builder.Property(aa => aa.IsPrimarySPOC)
            .HasDefaultValue(false);

        builder.Property(aa => aa.AssignedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(aa => aa.User)
            .WithMany(u => u.ApplicationAssignments)
            .HasForeignKey(aa => aa.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(aa => aa.Application)
            .WithMany(a => a.ApplicationAssignments)
            .HasForeignKey(aa => aa.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(aa => new { aa.UserId, aa.ApplicationId })
            .IsUnique();
    }
}
