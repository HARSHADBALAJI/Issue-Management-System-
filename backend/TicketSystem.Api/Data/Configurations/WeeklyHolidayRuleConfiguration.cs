using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Configurations;

public class WeeklyHolidayRuleConfiguration : IEntityTypeConfiguration<WeeklyHolidayRule>
{
    public void Configure(EntityTypeBuilder<WeeklyHolidayRule> builder)
    {
        builder.ToTable("WeeklyHolidayRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DayOfWeek).IsRequired();
        builder.Property(x => x.WeekType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
