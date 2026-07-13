namespace TicketSystem.Api.Models.DTOs.Common;

public class QueryParams
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 200;
    public string? SortBy { get; set; }
    public string SortDir { get; set; } = "asc";
}
