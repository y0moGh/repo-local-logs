using Maintenance.Core.Procesos;
using System.ServiceProcess;

namespace Maintenance.SVC
{
    public partial class Service1 : ServiceBase
    {
        DiskStatusProcess diskStatus = new DiskStatusProcess();
        public Service1()
        {
            InitializeComponent();

#if DEBUG
            OnTest();
#endif
        }

        protected override void OnStart(string[] args)
        {
            diskStatus.Start();
        }

        protected override void OnStop()
        {
            diskStatus.Stop();
        }

        private void OnTest()
        {
            diskStatus.Test();
        }
    }
}
