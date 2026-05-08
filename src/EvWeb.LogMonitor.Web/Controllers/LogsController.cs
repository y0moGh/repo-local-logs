using EvWeb.LogMonitor.Application.Contracts;
using EvWeb.LogMonitor.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace EvWeb.LogMonitor.Web.Controllers;

public class LogsController : Controller
{
    private readonly ILogDashboardService _logDashboardService;

    public LogsController(ILogDashboardService logDashboardService)
    {
        _logDashboardService = logDashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? source,
        string? folder,
        string? file,
        string? text,
        string? dateFrom,
        string? dateTo,
        string? association,
        string? logType,
        int maxLines = 500)
    {
        ViewData["Title"] = "Logs";
        var model = await BuildViewModelAsync(source, folder, file, text, dateFrom, dateTo, association, logType, maxLines);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(
        string? source,
        string? folder,
        string? file,
        string? text,
        string? dateFrom,
        string? dateTo,
        string? association,
        string? logType,
        int maxLines = 500)
    {
        var model = await BuildViewModelAsync(source, folder, file, text, dateFrom, dateTo, association, logType, maxLines);
        return PartialView("_DashboardContent", model);
    }

    [HttpGet]
    public async Task<IActionResult> LinesBatch(
        string? source,
        string? folder,
        string? file,
        string? text,
        string? dateFrom,
        string? dateTo,
        string? association,
        string? logType,
        int maxLines = 500,
        int offset = 0)
    {
        var model = await BuildViewModelAsync(source, folder, file, text, dateFrom, dateTo, association, logType, maxLines, offset);
        return PartialView("_LogLineBatch", model);
    }

    [HttpGet]
    public async Task<IActionResult> Download(string? source, string? folder, string file)
    {
        var result = await _logDashboardService.GetDownloadStreamAsync(source, folder, file);
        if (result is null || result.Value.Stream is null)
        {
            return NotFound();
        }

        return File(result.Value.Stream, "text/plain", result.Value.FileName);
    }

    private Task<LogDashboardViewModel> BuildViewModelAsync(
        string? source,
        string? folder,
        string? file,
        string? text,
        string? dateFrom,
        string? dateTo,
        string? association,
        string? logType,
        int maxLines,
        int offset = 0)
    {
        return _logDashboardService.GetDashboardAsync(new LogDashboardQuery
        {
            Source = source,
            Folder = folder,
            FileName = file,
            Text = text,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Association = association,
            LogType = logType,
            MaxLines = maxLines,
            Offset = offset
        });
    }
}
