using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data;

public class TicketSystemDbContext : DbContext
{
    public TicketSystemDbContext(DbContextOptions<TicketSystemDbContext> options) : base(options) { }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Requester> Requesters => Set<Requester>();
    public DbSet<TicketStatus> TicketStatuses => Set<TicketStatus>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationAssignment> ApplicationAssignments => Set<ApplicationAssignment>();
    public DbSet<ApplicationRoutingRule> ApplicationRoutingRules => Set<ApplicationRoutingRule>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketStatusHistory> TicketStatusHistories => Set<TicketStatusHistory>();
    public DbSet<TicketCorrectiveAction> TicketCorrectiveActions => Set<TicketCorrectiveAction>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<SlaSetting> SlaSettings => Set<SlaSetting>();
    public DbSet<HolidayCalendar> HolidayCalendars => Set<HolidayCalendar>();
    public DbSet<WeeklyHolidayRule> WeeklyHolidayRules => Set<WeeklyHolidayRule>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<TicketSla> TicketSlas => Set<TicketSla>();
    public DbSet<SlaAuditLog> SlaAuditLogs => Set<SlaAuditLog>();
    public DbSet<TicketAccessToken> TicketAccessTokens => Set<TicketAccessToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketSystemDbContext).Assembly);
    }
}
