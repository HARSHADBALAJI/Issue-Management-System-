namespace TicketSystem.Api.Models.DTOs.Tickets;

public class TrendsResponse
{
    public List<string> Labels { get; set; } = new();
    public List<SeriesData> Series { get; set; } = new();

    public class SeriesData
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<int> Data { get; set; } = new();
    }
}
