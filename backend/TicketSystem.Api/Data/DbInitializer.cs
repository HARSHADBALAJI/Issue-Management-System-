using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(TicketSystemDbContext context)
    {
        await context.Database.MigrateAsync();

        // seed essential reference data if missing
        if (!await context.Roles.AnyAsync())
        {
            context.Roles.AddRange(
                new Role { Name = "Admin", Description = "Full system access" },
                new Role { Name = "SPOC", Description = "Ticket operations only" }
            );
            await context.SaveChangesAsync();
        }

        if (!await context.TicketStatuses.AnyAsync())
        {
            context.TicketStatuses.AddRange(
                new TicketStatus { Id = 1, Name = "in_progress", DisplayName = "In Progress", Color = "#FFC107", SortOrder = 1 },
                new TicketStatus { Id = 2, Name = "waiting", DisplayName = "Waiting", Color = "#FD7E14", SortOrder = 2 },
                new TicketStatus { Id = 3, Name = "resolved", DisplayName = "Resolved", Color = "#198754", SortOrder = 3 },
                new TicketStatus { Id = 4, Name = "closed", DisplayName = "Closed", Color = "#6C757D", SortOrder = 4 },
                new TicketStatus { Id = 5, Name = "open", DisplayName = "Open", Color = "#0D6EFD", SortOrder = 0 }
            );
            await context.SaveChangesAsync();
        }

        await context.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = 'TicketSequence')
                CREATE SEQUENCE TicketSequence
                    START WITH 1001
                    INCREMENT BY 1
                    NO CYCLE;
        ");

        // ensure default department exists
        if (!await context.Departments.AnyAsync())
        {
            context.Departments.Add(new Department { Name = "General", IsActive = true });
            await context.SaveChangesAsync();
        }

        // ensure Unknown application exists for email fallback
        if (!await context.Applications.AnyAsync(a => a.Name == "Unknown"))
        {
            context.Applications.Add(new Application { Name = "Unknown", IsActive = true });
            await context.SaveChangesAsync();
        }

        // ensure admin user exists
        if (!await context.Users.AnyAsync(u => u.Email == "admin@ticketingsystem.com"))
        {
            var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
            var dept = await context.Departments.FirstAsync();
            context.Users.Add(new User
            {
                EmployeeId = "ADMIN001",
                FullName = "System Admin",
                Email = "admin@ticketingsystem.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                DepartmentId = dept.Id,
                RoleId = adminRole.Id,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        // Backfill first messages for existing tickets that have a description but no TicketMessage
        // This ensures TicketMessages is the single source of truth for all conversation messages
        var ticketsWithoutMessages = await context.Tickets
            .Where(t => !string.IsNullOrWhiteSpace(t.Description)
                     && !context.TicketMessages.Any(m => m.TicketId == t.Id))
            .ToListAsync();

        foreach (var ticket in ticketsWithoutMessages)
        {
            context.TicketMessages.Add(new TicketMessage
            {
                TicketId = ticket.Id,
                RequesterId = ticket.RequesterId,
                MessageSourceType = "Requester",
                Content = ticket.Description,
                IsInternal = false,
                CreatedAt = ticket.CreatedAt
            });
        }

        if (ticketsWithoutMessages.Count > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"Backfilled {ticketsWithoutMessages.Count} first messages for existing tickets.");
        }

        // Seed SLA settings
        if (!await context.SlaSettings.AnyAsync())
        {
            context.SlaSettings.Add(new SlaSetting
            {
                WorkStartTime = new TimeSpan(9, 0, 0),
                WorkEndTime = new TimeSpan(17, 40, 0),
                NotifyBeforeHours = 24,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        // Seed SLA policies
        if (!await context.SlaPolicies.AnyAsync())
        {
            context.SlaPolicies.AddRange(
                new SlaPolicy { Priority = "critical", DurationDays = 1, IsActive = true },
                new SlaPolicy { Priority = "high", DurationDays = 3, IsActive = true },
                new SlaPolicy { Priority = "medium", DurationDays = 5, IsActive = true },
                new SlaPolicy { Priority = "low", DurationDays = 7, IsActive = true }
            );
            await context.SaveChangesAsync();
        }

        // Seed weekly holiday rules
        if (!await context.WeeklyHolidayRules.AnyAsync())
        {
            context.WeeklyHolidayRules.AddRange(
                new WeeklyHolidayRule { DayOfWeek = DayOfWeek.Sunday, WeekType = "All", Description = "Every Sunday", IsActive = true },
                new WeeklyHolidayRule { DayOfWeek = DayOfWeek.Saturday, WeekType = "EverySecondAndFourth", Description = "2nd and 4th Saturday", IsActive = true }
            );
            await context.SaveChangesAsync();
        }
    }
}
