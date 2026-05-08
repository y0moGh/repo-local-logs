using EvWeb.LogMonitor.Domain.Entities;

namespace EvWeb.LogMonitor.Infrastructure.Configuration;

public class LogMonitorOptions
{
    public const string SectionName = "LogMonitor";

    public List<LogSourceDefinition> Sources { get; set; } = new();
}
