using Evweb.Code.Procesos.Mantenimiento;
using Serilog;
using System;
using System.Timers;

namespace Maintenance.Core.Procesos
{
    public class DiskStatusProcess
    {
        private static Timer diskStatusTimer { get; set; }

        private void PreStart()
        {
            new DiskStatus().Start();
        }

        public void Test()
        {
            new DiskStatus().Start();
        }

        public void Start()
        {
            try
            {
                PreStart();

                diskStatusTimer = new Timer(300000);
                diskStatusTimer.Elapsed += new ElapsedEventHandler(DiskStatusTimer_Elapsed);
                diskStatusTimer.Enabled = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ha ocurrido un error al iniciar el proceso de estado de disco");
            }
        }

        public void Stop()
        {
            try
            {
                diskStatusTimer.Dispose();

                Log.Information("Proceso de estado de disco finalizado");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ha ocurrido un error al detener el servicio de estado de disco");
            }
        }

        [STAThread]
        private static void DiskStatusTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                new DiskStatus().Start();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ha ocurrido un error al intentar ejecutar el servicio de estado de disco");
            }
        }


    }
}
