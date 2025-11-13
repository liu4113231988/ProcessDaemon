using Microsoft.Extensions.Options;
using Quartz;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProcessDaemon
{
    public class DaemonJob : IJob
    {
        private readonly ILogger<DaemonJob> _logger;
        private readonly string configFilePath = "daemon-config.json";
        IConfiguration _configuration;
        Config _config;
        public DaemonJob(ILogger<DaemonJob> logger, IConfiguration configuration, Config config )
        {
            _logger = logger;
            _configuration = configuration;
            _config = config;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            //Config config = new Config();
            //_configuration.Bind(config);
            if (_config.Profiles.Count > 0)
            {

                //Config config = JsonSerializer.Deserialize<Config>(content) ?? throw new Exception("Reading config error.");
                Process[] processes = Process.GetProcesses();
                foreach (Profile profile in _config.Profiles)
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
                        //processStartInfo.RedirectStandardInput = !UseShellExecuteCheckBox.Checked;
                        //processStartInfo.RedirectStandardOutput = !UseShellExecuteCheckBox.Checked;
                        //processStartInfo.RedirectStandardError = !UseShellExecuteCheckBox.Checked;
                        try
                        {
                            process = Process.Start(processStartInfo);
                            Log.Information($"Process {profile.Name} started successful.");
                            //process.Exited += Process_Exited;
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

        private void Process_Exited(object? sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }
    }
}
