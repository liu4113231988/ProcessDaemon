using Microsoft.Extensions.Options;
using Quartz;
using Serilog;
using System.Diagnostics;

namespace ProcessDaemon
{
    public class DaemonJob : IJob
    {
        private readonly ILogger<DaemonJob> _logger;
        private List<Profile> _profileList;
        public DaemonJob(ILogger<DaemonJob> logger, IOptions<List<Profile>> profiles)
        {
            _logger = logger;
            _profileList = profiles.Value;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            var profileList = _profileList?.Where(t => t.Enabled && t.Type == 1).ToList();
            if (profileList != null && profileList.Count > 0)
            {
                Process[] processes = Process.GetProcesses();
                foreach (Profile profile in profileList)
                {
                    var process = processes.Where(t => t.ProcessName == profile.Name).FirstOrDefault();
                    if (process == null)
                    {
                        ProcessStartInfo processStartInfo = new();
                        processStartInfo.FileName = profile.FileName;
                        processStartInfo.Arguments = profile.Arguments;
                        processStartInfo.WorkingDirectory = profile.WorkingDirectory;

                        processStartInfo.UseShellExecute = profile.UseShellExecute;
                        processStartInfo.CreateNoWindow = profile.CreateNoWindow;
                        try
                        {
                            process = Process.Start(processStartInfo);
                            Log.Information($"Process {profile.Name} started successful.");
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Process {profile.Name} start failed for reason:{ex.Message}");
                        }
                    }
                }
                await Task.CompletedTask;
            }
        }

    }
}
