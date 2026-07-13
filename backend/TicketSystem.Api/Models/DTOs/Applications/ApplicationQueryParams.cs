using TicketSystem.Api.Models.DTOs.Common;

namespace TicketSystem.Api.Models.DTOs.Applications;

public class ApplicationQueryParams : QueryParams
{
    public bool? IsActive { get; set; }
}
