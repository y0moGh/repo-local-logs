using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using EvWeb.LogMonitor.Application.Contracts;
using EvWeb.LogMonitor.Application.Models;
using EvWeb.LogMonitor.Domain.Constants;
using EvWeb.LogMonitor.Domain.Entities;
using EvWeb.LogMonitor.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EvWeb.LogMonitor.Infrastructure.Services;

public class FileSystemLogDashboardService : ILogDashboardService
{
    private static readonly string[] LogTypeOptions =
    {
        "Errors",
        "Warnings",
        "Info",
        "Sin tipo"
    };

    private static readonly Regex SerilogLineRegex = new(
        @"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?(?: [+-]\d{2}:\d{2})?) \[(?<category>[A-Z]{3})\] (?<message>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex BracketLineRegex = new(
        @"^\[\s*(?<category>[^|\]]+)\|\s*(?<ts>[^\]]+)\]\s*(?<message>.+)$",
        RegexOptions.Compiled);
    private static readonly Regex TimeInBracketRegex = new(
        @"^\[\s*(?:(?<association>[^|\]]+)\|\s*)?(?<time>\d{1,2}:\d{2}(?::\d{2}(?:\.\d+)?)?)\s*\]",
        RegexOptions.Compiled);

    private static readonly Regex TypeTokenRegex = new(
        @"Type\s*:\s*(?<type>[A-Za-z]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IReadOnlyList<LogSourceDefinition> _sources;

    public FileSystemLogDashboardService(IOptions<LogMonitorOptions> options)
    {
        _sources = options.Value.Sources.Count > 0
            ? options.Value.Sources
            : new List<LogSourceDefinition>
            {
                new() { Name = "Web", Path = "logs" },
                new() { Name = "Api", Path = "logs" },
                new() { Name = "Servicio", Path = "logs" }
            };
    }

    public async Task<LogDashboardViewModel> GetDashboardAsync(LogDashboardQuery query)
    {
        var selectedSource = ResolveSource(query.Source);
        var requestedOffset = Math.Max(0, query.Offset);
        var availableFolders = GetAvailableFolders(selectedSource.Path);
        var selectedFolder = ResolveSelectedFolder(query.Folder, availableFolders);
        var folderPath = GetFolderPath(selectedSource.Path, selectedFolder);
        var allAvailableFiles = await GetAvailableFilesAsync(folderPath);
        var availableFiles = ApplyFileDateFilters(allAvailableFiles, query.DateFrom, query.DateTo);

        var model = new LogDashboardViewModel
        {
            Sources = _sources.ToList(),
            AvailableFolders = availableFolders,
            AvailableFiles = availableFiles,
            AvailableLogTypes = LogTypeOptions.ToList(),
            SelectedSource = selectedSource.Name,
            SelectedFolder = selectedFolder,
            SelectedFile = query.FileName,
            TextFilter = query.Text,
            SelectedDateFrom = NormalizeFilterValue(query.DateFrom),
            SelectedDateTo = NormalizeFilterValue(query.DateTo),
            SelectedTimeFrom = TryParseHourMinute(NormalizeFilterValue(query.TimeFrom), out var queryTimeFrom) ? queryTimeFrom : null,
            SelectedTimeTo = TryParseHourMinute(NormalizeFilterValue(query.TimeTo), out var queryTimeTo) ? queryTimeTo : null,
            SelectedAssociation = NormalizeFilterValue(query.Association),
            SelectedLogType = NormalizeLogTypeFilter(query.LogType),
            MaxLines = NormalizeMaxLines(query.MaxLines),
            CurrentOffset = requestedOffset,
            SourcePath = selectedSource.Path,
            CurrentFolderPath = folderPath,
            IsConnected = Directory.Exists(folderPath)
        };

        if (!Directory.Exists(folderPath))
        {
            model.ErrorMessage = $"La carpeta '{folderPath}' no existe.";
            model.ContentSignature = BuildContentSignature(model);
            return model;
        }

        if (availableFiles.Count == 0)
        {
            model.SelectedFile = null;
            model.SelectedAssociation = null;
            model.SelectedLogType = null;
            model.AvailableAssociations = new List<string>();
            model.AvailableLogTypes = LogTypeOptions.ToList();
            model.TotalLines = 0;
            model.AvailableEntriesCount = 0;
            model.DisplayedLinesCount = 0;
            model.NextOffset = 0;
            model.HasMoreEntries = false;
            model.ErrorMessage = $"No se encontraron logs en '{folderPath}'.";
            model.ContentSignature = BuildContentSignature(model);
            return model;
        }

        var selectedFile = ResolveSelectedFile(query.FileName, availableFiles);
        model.SelectedFile = selectedFile?.FileName;

        if (selectedFile is null)
        {
            model.ErrorMessage = "No hay archivos de log disponibles.";
            model.ContentSignature = BuildContentSignature(model);
            return model;
        }

        var safePath = ResolveSafeFilePath(selectedSource.Path, selectedFolder, selectedFile.FileName);
        if (safePath is null)
        {
            model.ErrorMessage = "Archivo no valido.";
            model.ContentSignature = BuildContentSignature(model);
            return model;
        }

        try
        {
            string[] allLines;
            using (var stream = new FileStream(safePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync();
                allLines = content
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            model.TotalLines = allLines.Length;
            var parsedEntries = allLines
                .Select(ParseLine)
                .Select(NormalizeEntryTimestamp)
                .Select(entry => NormalizeEntryCategory(entry, selectedFolder))
                .ToList();

            model.AvailableAssociations = parsedEntries
                .Select(entry => entry.Association)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!TryParseDateOnly(model.SelectedDateFrom, out var selectedDateFrom))
            {
                model.SelectedDateFrom = null;
                selectedDateFrom = null;
            }

            if (!TryParseDateOnly(model.SelectedDateTo, out var selectedDateTo))
            {
                model.SelectedDateTo = null;
                selectedDateTo = null;
            }

            if (selectedDateFrom.HasValue && selectedDateTo.HasValue && selectedDateFrom > selectedDateTo)
            {
                (selectedDateFrom, selectedDateTo) = (selectedDateTo, selectedDateFrom);
                model.SelectedDateFrom = selectedDateFrom.Value.ToString("yyyy-MM-dd");
                model.SelectedDateTo = selectedDateTo.Value.ToString("yyyy-MM-dd");
            }

            var selectedTimeFrom = model.SelectedTimeFrom;
            var selectedTimeTo = model.SelectedTimeTo;

            if (selectedTimeFrom.HasValue && selectedTimeTo.HasValue && selectedTimeFrom > selectedTimeTo)
            {
                (selectedTimeFrom, selectedTimeTo) = (selectedTimeTo, selectedTimeFrom);
                model.SelectedTimeFrom = selectedTimeFrom;
                model.SelectedTimeTo = selectedTimeTo;
            }

            if (!string.IsNullOrWhiteSpace(model.SelectedAssociation)
                && !model.AvailableAssociations.Contains(model.SelectedAssociation, StringComparer.OrdinalIgnoreCase))
            {
                model.SelectedAssociation = null;
            }

            if (!string.IsNullOrWhiteSpace(model.SelectedLogType)
                && !LogTypeOptions.Contains(model.SelectedLogType, StringComparer.OrdinalIgnoreCase))
            {
                model.SelectedLogType = null;
            }

            var filteredEntries = parsedEntries
                .Where(entry => string.IsNullOrWhiteSpace(model.TextFilter)
                    || entry.RawLine.Contains(model.TextFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry => IsInDateRange(entry, selectedDateFrom, selectedDateTo))
                .Where(entry => IsInTimeRange(entry, selectedTimeFrom, selectedTimeTo))
                .Where(entry => string.IsNullOrWhiteSpace(model.SelectedAssociation)
                    || entry.Association.Equals(model.SelectedAssociation, StringComparison.OrdinalIgnoreCase))
                .Where(entry => MatchesLogType(entry, model.SelectedLogType))
                .ToList();

            model.AvailableEntriesCount = filteredEntries.Count;
            model.Entries = filteredEntries
                .Skip(requestedOffset)
                .Take(model.MaxLines)
                .ToList();

            model.DisplayedLinesCount = Math.Min(model.AvailableEntriesCount, requestedOffset + model.Entries.Count);
            model.NextOffset = model.DisplayedLinesCount;
            model.HasMoreEntries = model.NextOffset < model.AvailableEntriesCount;
        }
        catch (FileNotFoundException)
        {
            model.ErrorMessage = $"Archivo no encontrado: {selectedFile.FileName}";
        }
        catch (Exception ex)
        {
            model.ErrorMessage = $"Error al leer el log: {ex.Message}";
        }

        model.ContentSignature = BuildContentSignature(model);
        return model;
    }

    public Task<(Stream? Stream, string FileName)?> GetDownloadStreamAsync(string? source, string? folder, string fileName)
    {
        var selectedSource = ResolveSource(source);
        var selectedFolder = LogFolders.Normalize(folder);
        var safePath = ResolveSafeFilePath(selectedSource.Path, selectedFolder, fileName);

        if (safePath is null || !File.Exists(safePath))
        {
            return Task.FromResult<(Stream? Stream, string FileName)?>(null);
        }

        Stream stream = new FileStream(safePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Task.FromResult<(Stream? Stream, string FileName)?>((stream, Path.GetFileName(safePath)));
    }

    private LogSourceDefinition ResolveSource(string? source)
    {
        return _sources.FirstOrDefault(item => item.Name.Equals(source, StringComparison.OrdinalIgnoreCase))
            ?? _sources.First();
    }

    private static async Task<List<LogFileDescriptor>> GetAvailableFilesAsync(string folderPath)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(folderPath))
            {
                return new List<LogFileDescriptor>();
            }

            return Directory.GetFiles(folderPath, "*.log")
                .Concat(Directory.GetFiles(folderPath, "*.Log"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new LogFileDescriptor
                {
                    FileName = Path.GetFileName(path),
                    CreationTime = File.GetCreationTime(path),
                    LastWriteTime = File.GetLastWriteTime(path)
                })
                .OrderByDescending(item => item.LastWriteTime)
                .ToList();
        });
    }

    private static LogFileDescriptor? ResolveSelectedFile(string? fileName, List<LogFileDescriptor> availableFiles)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var selected = availableFiles.FirstOrDefault(item =>
                item.FileName.Equals(Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase));

            if (selected is not null)
            {
                return selected;
            }
        }

        return availableFiles.FirstOrDefault();
    }

    private static string? ResolveSafeFilePath(string sourcePath, string folder, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !LogFolders.IsValid(folder))
            return null;

        var basePath = Path.GetFullPath(GetFolderPath(sourcePath, folder));
        var candidatePath = Path.GetFullPath(Path.Combine(basePath, Path.GetFileName(fileName)));

        // Sin cambios — ya funciona correctamente con el nuevo GetFolderPath
        return candidatePath.StartsWith(basePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || candidatePath.Equals(basePath, StringComparison.OrdinalIgnoreCase)
            ? candidatePath
            : null;
    }

    private static string GetFolderPath(string sourcePath, string folder)
    {
        if (LogFolders.UsesBasePath(folder))
            return sourcePath;

        return Path.Combine(sourcePath, LogFolders.Normalize(folder));
    }

    private static List<string> GetAvailableFolders(string sourcePath)
    {
        var availableFolders = new List<string> { LogFolders.Legacy };
        var optionalFolders = new[]
        {
            LogFolders.Errors,
            LogFolders.Warnings,
            LogFolders.Info
        };

        availableFolders.AddRange(optionalFolders.Where(folder => FolderExistsForSource(sourcePath, folder)));
        return availableFolders;
    }

    private static bool FolderExistsForSource(string sourcePath, string folder)
    {
        if (LogFolders.UsesBasePath(folder))
        {
            return Directory.Exists(sourcePath);
        }

        return Directory.Exists(Path.Combine(sourcePath, folder));
    }

    private static string ResolveSelectedFolder(string? requestedFolder, List<string> availableFolders)
    {
        var normalizedFolder = LogFolders.Normalize(requestedFolder);
        if (availableFolders.Contains(normalizedFolder, StringComparer.OrdinalIgnoreCase))
        {
            return normalizedFolder;
        }

        return LogFolders.Legacy;
    }

    private static int NormalizeMaxLines(int maxLines)
    {
        var allowedValues = new[] { 100, 250, 500, 1000, 2000 };
        return allowedValues.Contains(maxLines) ? maxLines : 500;
    }

    private static LogEntry ParseLine(string line)
    {
        var serilogMatch = SerilogLineRegex.Match(line);
        if (serilogMatch.Success)
        {
            return new LogEntry
            {
                Timestamp = serilogMatch.Groups["ts"].Value,
                Category = serilogMatch.Groups["category"].Value,
                Message = serilogMatch.Groups["message"].Value,
                Association = string.Empty,
                RawLine = line
            };
        }

        var bracketMatch = BracketLineRegex.Match(line);
        if (bracketMatch.Success)
        {
            return new LogEntry
            {
                Timestamp = bracketMatch.Groups["ts"].Value.Trim(),
                Category = string.Empty,
                Association = bracketMatch.Groups["category"].Value.Trim(),
                Message = bracketMatch.Groups["message"].Value,
                RawLine = line
            };
        }

        return new LogEntry
        {
            Timestamp = string.Empty,
            Association = string.Empty,
            Category = "",
            Message = line,
            RawLine = line
        };
    }

    private static LogEntry NormalizeEntryCategory(LogEntry entry, string selectedFolder)
    {
        var explicitType = TryExtractLogType(entry.RawLine);
        if (!string.IsNullOrWhiteSpace(explicitType))
        {
            entry.Category = explicitType;
            return entry;
        }

        entry.Category = NormalizeLogType(entry.Category) ?? string.Empty;
        return entry;
    }

    private static LogEntry NormalizeEntryTimestamp(LogEntry entry)
    {
        if (DateTimeOffset.TryParse(entry.Timestamp, out var offsetTimestamp))
        {
            entry.OccurredAt = offsetTimestamp.LocalDateTime;
            entry.Timestamp = offsetTimestamp.ToString("dd/MM/yy HH:mm:ss");
            return entry;
        }

        if (DateTime.TryParse(entry.Timestamp, out var fullTimestamp))
        {
            entry.OccurredAt = fullTimestamp;
            entry.Timestamp = fullTimestamp.ToString("dd/MM/yy HH:mm:ss");
            return entry;
        }

        entry.OccurredAt = null;
        entry.Timestamp = string.IsNullOrWhiteSpace(entry.Timestamp)
            ? string.Empty
            : entry.Timestamp.Trim();

        return entry;
    }

    private static string? NormalizeFilterValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Equals("Todos", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Clear", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Limpiar", StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();
    }

    private static string? NormalizeLogTypeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Equals("Todos", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return NormalizeLogType(value) ?? (value.Equals("Sin tipo", StringComparison.OrdinalIgnoreCase) ? "Sin tipo" : null);
    }

    private static string? TryExtractLogType(string line)
    {
        var match = TypeTokenRegex.Match(line);
        if (!match.Success)
        {
            return null;
        }

        return NormalizeLogType(match.Groups["type"].Value);
    }

    private static string? NormalizeLogType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            var type when type.Equals("ERR", StringComparison.OrdinalIgnoreCase) => "Errors",
            var type when type.Equals("ERROR", StringComparison.OrdinalIgnoreCase) => "Errors",
            var type when type.Equals("ERRORS", StringComparison.OrdinalIgnoreCase) => "Errors",
            var type when type.Equals("WRN", StringComparison.OrdinalIgnoreCase) => "Warnings",
            var type when type.Equals("WARNING", StringComparison.OrdinalIgnoreCase) => "Warnings",
            var type when type.Equals("WARNINGS", StringComparison.OrdinalIgnoreCase) => "Warnings",
            var type when type.Equals("INF", StringComparison.OrdinalIgnoreCase) => "Info",
            var type when type.Equals("INFO", StringComparison.OrdinalIgnoreCase) => "Info",
            var type when type.Equals("PROCESS", StringComparison.OrdinalIgnoreCase) => "Info",
            _ => null
        };
    }

    private static bool MatchesLogType(LogEntry entry, string? selectedLogType)
    {
        if (string.IsNullOrWhiteSpace(selectedLogType))
        {
            return true;
        }

        if (selectedLogType.Equals("Sin tipo", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(entry.Category);
        }

        return string.Equals(entry.Category, selectedLogType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseDateOnly(string? value, out DateOnly? parsedDate)
    {
        if (DateOnly.TryParse(value, out var date))
        {
            parsedDate = date;
            return true;
        }

        parsedDate = null;
        return false;
    }

    private static bool IsInDateRange(LogEntry entry, DateOnly? from, DateOnly? to)
    {
        if (!from.HasValue && !to.HasValue)
        {
            return true;
        }

        if (!entry.OccurredAt.HasValue)
        {
            return false;
        }

        var entryDate = DateOnly.FromDateTime(entry.OccurredAt.Value);

        if (from.HasValue && entryDate < from.Value)
        {
            return false;
        }

        if (to.HasValue && entryDate > to.Value)
        {
            return false;
        }

        return true;
    }

    private static List<LogFileDescriptor> ApplyFileDateFilters(List<LogFileDescriptor> files, string? dateFromFilter, string? dateToFilter)
    {
        TryParseDateOnly(NormalizeFilterValue(dateFromFilter), out var dateFrom);
        TryParseDateOnly(NormalizeFilterValue(dateToFilter), out var dateTo);

        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
        {
            (dateFrom, dateTo) = (dateTo, dateFrom);
        }

        if (!dateFrom.HasValue && !dateTo.HasValue)
        {
            return files;
        }

        return files
            .Where(file =>
            {
                var creationDate = DateOnly.FromDateTime(file.CreationTime);

                if (dateFrom.HasValue && creationDate < dateFrom.Value)
                {
                    return false;
                }

                if (dateTo.HasValue && creationDate > dateTo.Value)
                {
                    return false;
                }

                return true;
            })
            .ToList();
    }

    private static bool TryParseHourMinute(string? value, out TimeOnly? parsedTime)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsedTime = null;
            return false;
        }

        if (TimeOnly.TryParseExact(value.Trim(), "HH:mm", out var exactTime)
            || TimeOnly.TryParse(value.Trim(), out exactTime))
        {
            parsedTime = new TimeOnly(exactTime.Hour, exactTime.Minute);
            return true;
        }

        parsedTime = null;
        return false;
    }

    private static bool IsInTimeRange(LogEntry entry, TimeOnly? from, TimeOnly? to)
    {
        if (!from.HasValue && !to.HasValue)
        {
            return true;
        }

        if (!TryExtractEntryHourMinute(entry, out var entryTime))
        {
            return false;
        }

        if (from.HasValue && entryTime < from.Value)
        {
            return false;
        }

        if (to.HasValue && entryTime > to.Value)
        {
            return false;
        }

        return true;
    }

    private static bool TryExtractEntryHourMinute(LogEntry entry, out TimeOnly time)
    {
        var bracketMatch = TimeInBracketRegex.Match(entry.RawLine);
        if (bracketMatch.Success
            && TimeOnly.TryParse(bracketMatch.Groups["time"].Value.Trim(), out var bracketTime))
        {
            time = new TimeOnly(bracketTime.Hour, bracketTime.Minute);
            return true;
        }

        if (entry.OccurredAt.HasValue)
        {
            time = new TimeOnly(entry.OccurredAt.Value.Hour, entry.OccurredAt.Value.Minute);
            return true;
        }

        if (TimeOnly.TryParse(entry.Timestamp, out var parsedTime))
        {
            time = new TimeOnly(parsedTime.Hour, parsedTime.Minute);
            return true;
        }

        time = default;
        return false;
    }

    private static string BuildContentSignature(LogDashboardViewModel model)
    {
        var builder = new StringBuilder();
        builder.Append(model.SelectedSource).Append('|');
        builder.Append(model.SelectedFolder).Append('|');
        builder.Append(model.SelectedFile).Append('|');
        builder.Append(model.SelectedDateFrom).Append('|');
        builder.Append(model.SelectedDateTo).Append('|');
        builder.Append(model.SelectedAssociation).Append('|');
        builder.Append(model.SelectedLogType).Append('|');
        builder.Append(model.TotalLines).Append('|');
        builder.Append(model.AvailableEntriesCount).Append('|');
        builder.Append(model.DisplayedLinesCount).Append('|');
        builder.Append(model.ErrorMessage).Append('|');

        foreach (var entry in model.Entries)
        {
            builder.Append(entry.Timestamp).Append('\u001f');
            builder.Append(entry.Category).Append('\u001f');
            builder.Append(entry.RawLine).Append('\u001e');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }
}
