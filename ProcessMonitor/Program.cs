using System;
using System.IO;
using System.Diagnostics;
using System.Timers;

namespace ProcessMonitor
{
    internal class Program
    {
        static string processName = "python", csvPath = "monitor.csv";
        static System.Timers.Timer timer;
        static void Main(string[] args)
        {
            timer = new();
            timer.Elapsed += Timer_Elapsed;
            timer.Interval = 30 * 1000;
            timer.Start();
            Console.WriteLine("开始监测进程,30s一次间隔----");
            Console.ReadKey();
        }

        private static void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            Process? pythonProcess = Process.GetProcesses().Where(t => t.ProcessName.ToLower() == processName).FirstOrDefault();
            if (pythonProcess != null)
            {
                using (pythonProcess)
                {
                    long memoryUsage = pythonProcess.WorkingSet64 / 1024 / 1024;
                    string processName = pythonProcess.ProcessName;
                    PerformanceCounter cpuCounter = new PerformanceCounter("Process", "% Processor Time", processName, true);
                    // 等待一小段时间以允许计数器初始化并收集一些数据
                    Thread.Sleep(1000);
                    // 读取CPU使用率
                    //PerformanceCounter memoryCounter = new PerformanceCounter("Process", "Working Set", processName, true);
                    //PerformanceCounter memoryCounter1 = new PerformanceCounter("Process", "Working Set - Private", processName, true);
                    //double d1 = memoryCounter.NextValue();
                    //double d2 = memoryCounter1.NextValue();
                    double cpuUsage = Math.Round(cpuCounter.NextValue() / Environment.ProcessorCount, 2);
                    WriteCsvFile($"{DateTimeOffset.Now.ToUnixTimeMilliseconds()},{processName},{cpuUsage},{memoryUsage}");
                }
            }
        }

        private static void WriteCsvFile(string text)
        {
            bool fileExist = File.Exists(csvPath);
            using (StreamWriter sw = new StreamWriter(csvPath, true))
            {
                if (!fileExist)
                {
                    sw.WriteLine("Timestamp,ProcessName,CPU%,Memory(MB)");
                }
                sw.WriteLine(text);
            }
        }
    }
}
