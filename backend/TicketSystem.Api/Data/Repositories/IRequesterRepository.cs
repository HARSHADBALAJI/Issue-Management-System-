using TicketSystem.Api.Models.Entities;

namespace TicketSystem.Api.Data.Repositories;

public interface IRequesterRepository : IRepository<Requester>
{
    Task<Requester?> GetByEmailAsync(string email);
}
