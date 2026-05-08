using Serilog;
using System;
using System.ServiceProcess;

namespace Maintenance.SVC
{
    internal static class Program
    {
        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File($"{AppDomain.CurrentDomain.BaseDirectory}/logs/log.txt", rollingInterval: RollingInterval.Day)

                .CreateLogger();

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service1()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
