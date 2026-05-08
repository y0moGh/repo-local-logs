using EvWeb.LogMonitor.Application.Contracts;
using EvWeb.LogMonitor.Infrastructure.Configuration;
using EvWeb.LogMonitor.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EvWeb.LogMonitor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LogMonitorOptions>(configuration.GetSection(LogMonitorOptions.SectionName));
        services.AddScoped<ILogDashboardService, FileSystemLogDashboardService>();
        return services;
    }
}
