using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class TicketAccessTokenConfiguration : IEntityTypeConfiguration<TicketAccessToken>
{
    public void Configure(EntityTypeBuilder<TicketAccessToken> builder)
    {
        builder.ToTable("TicketAccessTokens");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TokenHash).HasMaxLength(255);
        builder.Property(e => e.Email).HasMaxLength(255);
        builder.HasIndex(e => e.TokenHash);
        builder.HasIndex(e => new { e.TicketId, e.Email });
        builder.HasOne(e => e.Ticket)
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
