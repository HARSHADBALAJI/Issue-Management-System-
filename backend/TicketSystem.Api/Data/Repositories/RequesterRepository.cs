using Microsoft.EntityFrameworkCore;
using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public class RequesterRepository : Repository<Requester>, IRequesterRepository
{
    public RequesterRepository(TicketSystemDbContext context) : base(context) { }

    public async Task<Requester?> GetByEmailAsync(string email)
        => await Context.Requesters.FirstOrDefaultAsync(r => r.Email == email);
}
