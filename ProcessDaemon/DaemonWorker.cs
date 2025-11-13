using Quartz;
using Serilog;
using System.Diagnostics;
using System.Text.Json;

namespace ProcessDaemon
{
    public class DaemonWorker : BackgroundService
    {
        private readonly ILogger<DaemonWorker> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IConfiguration _configuration;

        private bool isServiceStop;
        private Config _config;

        public DaemonWorker(ILogger<DaemonWorker> logger, IHostApplicationLifetime hostApplicationLifetime,
            IConfiguration configuration, Config config)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _configuration = configuration;
            isServiceStop = false;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                foreach (var profile in _config.Profiles)
                {
                    if (profile.FileName.EndsWith(".exe"))
                    {
                        Process process = StartProcess(profile);
                        if (process != null)
                        {
                            process.EnableRaisingEvents = true;
                            profile.ProcessId = process.Id;
                            process.Exited += Process_Exited;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("ExecuteAsync Error:", ex);
            }

        }

        private Process StartProcess(Profile profile)
        {
            ProcessStartInfo processStartInfo = new();
            processStartInfo.FileName = profile.FileName;
            processStartInfo.Arguments = profile.Arguments;
            processStartInfo.WorkingDirectory = profile.WorkingDirectory;

            processStartInfo.UseShellExecute = profile.UseShellExecute;
            processStartInfo.CreateNoWindow = profile.CreateNoWindow;
            try
            {
                Process process = Process.Start(processStartInfo);
                Log.Information($"Process {profile.Name} started successful.");

                return process;
            }
            catch (Exception ex)
            {
                Log.Error($"Process {profile.Name} start failed for reason:{ex.Message}");
                return null;
            }
        }
        private void Process_Exited(object? sender, EventArgs e)
        {
            Process process = sender as Process;
            if (process != null)
            {
                int processId = process.Id;
                var temp = _config.Profiles.Where(t => t.ProcessId == processId).FirstOrDefault();
                //如果是关闭服务，则不重启应用
                if (temp != null && !isServiceStop && temp.AfterStopped == 1)
                {
                    Log.Information($"Application {temp.Name} has been shutdown, and begin to start.");
                    if (temp.DelayForSeconds > 0)
                    {
                        Task.Delay(temp.DelayForSeconds * 1000);
                    }
                    StartProcess(temp);

                }

            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            isServiceStop = true;
            return base.StopAsync(cancellationToken);
        }
    }
}
