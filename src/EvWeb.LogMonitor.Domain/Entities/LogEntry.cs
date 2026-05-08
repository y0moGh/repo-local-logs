namespace EvWeb.LogMonitor.Domain.Entities;

public class LogEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public DateTime? OccurredAt { get; set; }
    public string Association { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RawLine { get; set; } = string.Empty;
}
