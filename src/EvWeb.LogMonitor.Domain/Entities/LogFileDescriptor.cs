namespace EvWeb.LogMonitor.Domain.Entities;

public class LogFileDescriptor
{
    public string FileName { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
    public DateTime LastWriteTime { get; set; }
}
