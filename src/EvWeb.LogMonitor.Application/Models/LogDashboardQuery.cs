namespace EvWeb.LogMonitor.Application.Models;

public class LogDashboardQuery
{
    public string? Source { get; set; }
    public string? Folder { get; set; }
    public string? FileName { get; set; }
    public string? Text { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public string? Association { get; set; }
    public string? LogType { get; set; }
    public int MaxLines { get; set; } = 500;
    public int Offset { get; set; }
}
