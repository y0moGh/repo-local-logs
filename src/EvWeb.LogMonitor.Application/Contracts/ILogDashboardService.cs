using EvWeb.LogMonitor.Application.Models;

namespace EvWeb.LogMonitor.Application.Contracts;

public interface ILogDashboardService
{
    Task<LogDashboardViewModel> GetDashboardAsync(LogDashboardQuery query);
    Task<(Stream? Stream, string FileName)?> GetDownloadStreamAsync(string? source, string? folder, string fileName);
}
