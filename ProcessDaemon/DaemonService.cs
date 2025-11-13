using Quartz;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessDaemon
{
    internal class DaemonService : IHostedService
    {

        private readonly ISchedulerFactory _schedulerFactory;
        public DaemonService(ISchedulerFactory schedulerFactory)
        {
            _schedulerFactory = schedulerFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Information("Start job");
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            await scheduler.Start(cancellationToken);

        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Information("Stop job");
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            await scheduler.Shutdown(cancellationToken);
        }
    }
}
