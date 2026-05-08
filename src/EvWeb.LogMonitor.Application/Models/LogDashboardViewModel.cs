using EvWeb.LogMonitor.Domain.Entities;

namespace EvWeb.LogMonitor.Application.Models;

public class LogDashboardViewModel
{
    public List<LogEntry> Entries { get; set; } = new();
    public List<LogFileDescriptor> AvailableFiles { get; set; } = new();
    public List<LogSourceDefinition> Sources { get; set; } = new();
    public List<string> AvailableFolders { get; set; } = new();
    public List<string> AvailableAssociations { get; set; } = new();
    public List<string> AvailableLogTypes { get; set; } = new();
    public string SelectedSource { get; set; } = string.Empty;
    public string SelectedFolder { get; set; } = string.Empty;
    public string? SelectedFile { get; set; }
    public string? TextFilter { get; set; }
    public string? SelectedDateFrom { get; set; }
    public string? SelectedDateTo { get; set; }
    public string? SelectedTimeFrom { get; set; }
    public string? SelectedTimeTo { get; set; }
    public string? SelectedAssociation { get; set; }
    public string? SelectedLogType { get; set; }
    public int MaxLines { get; set; } = 500;
    public int TotalLines { get; set; }
    public int AvailableEntriesCount { get; set; }
    public int DisplayedLinesCount { get; set; }
    public int CurrentOffset { get; set; }
    public int NextOffset { get; set; }
    public bool HasMoreEntries { get; set; }
    public string? ErrorMessage { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string CurrentFolderPath { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string ContentSignature { get; set; } = string.Empty;
}
