using Microsoft.Extensions.Options;
using Quartz;
using Serilog;
using System.Collections.Concurrent;
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

        // Track running processes and their memory monitoring state
        private readonly ConcurrentDictionary<int, Process> _runningProcesses = new();
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _monitorCts = new();
        private readonly ConcurrentDictionary<int, ConcurrentQueue<DateTime>> _breachRecords = new();
        private readonly ConcurrentDictionary<int, Task> _monitorTasks = new();

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

                    // Track the process
                    _runningProcesses[process.Id] = process;

                    // Start memory monitoring if enabled (Type=2 only)
                    if (profile.MaxMemoryMB > 0)
                    {
                        StartMemoryMonitoring(process, profile);
                    }

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

        private void StartMemoryMonitoring(Process process, Profile profile)
        {
            var cts = new CancellationTokenSource();
            _monitorCts[process.Id] = cts;
            _breachRecords[process.Id] = new ConcurrentQueue<DateTime>();

            int checkInterval = profile.MemoryCheckIntervalSeconds > 0 ? profile.MemoryCheckIntervalSeconds : 10;
            int thresholdCount = profile.MemoryThresholdCount > 0 ? profile.MemoryThresholdCount : 3;
            int timeWindowSeconds = profile.MemoryTimeWindowSeconds > 0 ? profile.MemoryTimeWindowSeconds : 300;

            var monitorTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested && !isServiceStop)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(checkInterval), cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }

                    if (process.HasExited)
                    {
                        break;
                    }

                    try
                    {
                        process.Refresh();
                        long workingSetBytes = process.WorkingSet64;
                        double workingSetMB = workingSetBytes / (1024.0 * 1024.0);

                        if (workingSetMB > profile.MaxMemoryMB)
                        {
                            if (!_breachRecords.TryGetValue(process.Id, out var records))
                            {
                                // Monitoring state was cleaned up (process restarted elsewhere)
                                break;
                            }
                            records.Enqueue(DateTime.Now);

                            // Clean old records outside the time window
                            DateTime cutoff = DateTime.Now.AddSeconds(-timeWindowSeconds);
                            while (records.TryPeek(out DateTime oldest) && oldest < cutoff)
                            {
                                records.TryDequeue(out _);
                            }

                            Log.Warning($"Process {profile.Name} memory {workingSetMB:F1}MB exceeds limit {profile.MaxMemoryMB}MB. " +
                                $"Breach count: {records.Count}/{thresholdCount} in {timeWindowSeconds}s window.");

                            // Check if threshold reached
                            if (records.Count >= thresholdCount)
                            {
                                Log.Warning($"Process {profile.Name} memory threshold reached ({records.Count} times in {timeWindowSeconds}s). Restarting...");

                                // Clear records before restart
                                while (records.TryDequeue(out _)) { }

                                // Kill the process - Process_Exited handler will restart it
                                try
                                {
                                    if (!process.HasExited)
                                    {
                                        process.Kill(entireProcessTree: true);
                                        process.WaitForExit(5000);
                                    }
                                }
                                catch (Exception killEx)
                                {
                                    Log.Error($"Failed to kill process {profile.Name}: {killEx.Message}");
                                    // If kill fails, try restarting directly
                                    RestartProcess(profile);
                                }
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process exited between check and access
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Memory monitoring error for {profile.Name}: {ex.Message}");
                    }
                }
            }, cts.Token);

            _monitorTasks[process.Id] = monitorTask;
        }

        private void RestartProcess(Profile profile)
        {
            if (isServiceStop) return;

            Log.Information($"Restarting process {profile.Name} due to memory threshold.");
            if (profile.DelayForSeconds > 0)
            {
                Thread.Sleep(profile.DelayForSeconds * 1000);
            }

            // Clean up old monitoring state
            if (_monitorCts.TryRemove(profile.ProcessId, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }
            _runningProcesses.TryRemove(profile.ProcessId, out _);
            _breachRecords.TryRemove(profile.ProcessId, out _);
            _monitorTasks.TryRemove(profile.ProcessId, out _);

            // Start new process
            Process? process = StartProcess(profile);
            if (process != null)
            {
                process.EnableRaisingEvents = true;
                profile.ProcessId = process.Id;
            }
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            Process process = sender as Process;
            if (process != null)
            {
                int processId = process.Id;

                // Stop memory monitoring for this process
                if (_monitorCts.TryRemove(processId, out var cts))
                {
                    try { cts.Cancel(); } catch { }
                    try { cts.Dispose(); } catch { }
                }
                _runningProcesses.TryRemove(processId, out _);
                _breachRecords.TryRemove(processId, out _);
                _monitorTasks.TryRemove(processId, out _);

                var temp = _profileList.Where(t => t.Enabled && t.ProcessId == processId).FirstOrDefault();
                // 如果配置了自动重启
                if (temp != null && !isServiceStop && temp.AfterStopped == 1)
                {
                    Log.Information($"Application {temp.Name} has been shutdown, and begin to start.");
                    if (temp.DelayForSeconds > 0)
                    {
                        Thread.Sleep(temp.DelayForSeconds * 1000);
                    }
                    StartProcess(temp);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            isServiceStop = true;

            // Cancel all memory monitoring tasks
            foreach (var kvp in _monitorCts)
            {
                try { kvp.Value.Cancel(); } catch { }
                try { kvp.Value.Dispose(); } catch { }
            }
            _monitorCts.Clear();
            _breachRecords.Clear();
            _monitorTasks.Clear();

            return base.StopAsync(cancellationToken);
        }
    }
}
