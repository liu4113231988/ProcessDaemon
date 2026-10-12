using Microsoft.Extensions.Options;
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
        private List<Profile> _profileList;

        public DaemonWorker(ILogger<DaemonWorker> logger, IHostApplicationLifetime hostApplicationLifetime,
            IConfiguration configuration, IOptions<List<Profile>> profile)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
            _configuration = configuration;
            isServiceStop = false;
            _profileList = profile.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                Console.WriteLine("begin start");
                var profileList = _profileList?.Where(t => t.Enabled && t.Type == 2).ToList();
                if (profileList != null)
                {
                    foreach (var profile in profileList)
                    {
                        if (profile.FileName.EndsWith(".exe"))
                        {
                            Process? process = StartProcess(profile);
                            if (process != null)
                            {
                                process.EnableRaisingEvents = true;
                                profile.ProcessId = process.Id;
                            }
                        }
                    }
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Log.Error("ExecuteAsync Error:", ex);
            }

        }

        private Process? StartProcess(Profile profile)
        {
            ProcessStartInfo processStartInfo = new();
            processStartInfo.FileName = profile.FileName;
            processStartInfo.Arguments = profile.Arguments;
            processStartInfo.WorkingDirectory = profile.WorkingDirectory;

            processStartInfo.UseShellExecute = profile.UseShellExecute;
            processStartInfo.CreateNoWindow = profile.CreateNoWindow;
            try
            {
                Process? process = Process.Start(processStartInfo);
                if (process != null)
                {
                    Console.WriteLine($"Process {profile.Name} started successful.");
                    Log.Information($"Process {profile.Name} started successful.");
                    process.EnableRaisingEvents = true;
                    process.Exited += Process_Exited;
                    profile.ProcessId = process.Id;
                    return process;
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
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
                var temp = _profileList.Where(t => t.Enabled && t.ProcessId == processId).FirstOrDefault();
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
