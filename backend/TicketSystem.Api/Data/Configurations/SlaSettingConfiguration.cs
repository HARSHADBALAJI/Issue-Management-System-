using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class SlaSettingConfiguration : IEntityTypeConfiguration<SlaSetting>
{
    public void Configure(EntityTypeBuilder<SlaSetting> builder)
    {
        builder.ToTable("SlaSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WorkStartTime).HasColumnType("time").IsRequired();
        builder.Property(x => x.WorkEndTime).HasColumnType("time").IsRequired();
        builder.Property(x => x.NotifyBeforeHours).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
