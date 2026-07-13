using TicketSystem.Api.Models.DTOs.Common;

namespace TicketSystem.Api.Models.DTOs.Users;

public class UserQueryParams : QueryParams
{
    public int? RoleId { get; set; }
    public int? DepartmentId { get; set; }
    public bool? IsActive { get; set; }
}
