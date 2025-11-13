using Microsoft.Extensions.Configuration;
using Quartz;
using Quartz.Impl.Matchers;
using Serilog;

namespace ProcessDaemon
{
    public class Program
    {
        public static void Main(string[] args)
        {

            Log.Logger = new LoggerConfiguration()
               .Enrich.FromLogContext()
               .WriteTo.File("logs/log.log", outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
               .CreateLogger();

            var builder = Host.CreateApplicationBuilder(args);
            //builder.Services.AddHostedService<DaemonService>();
            builder.Services.AddWindowsService(option =>
            {
                option.ServiceName = "ProcessDaemon";
            });
            builder.Services.AddLogging();
            builder.Configuration.AddJsonFile("daemon-config.json");
            string jobType = builder.Configuration["Type"].ToString();
            Config config = new Config();
            builder.Configuration.Bind(config);
            builder.Services.AddSingleton(config);
            if (jobType == "1")
            {
                //builder.Services.AddTransient<DaemonJob>();
                builder.Services.AddQuartz(opt =>
                {
                    // these are the defaults
                    opt.UseSimpleTypeLoader();
                    opt.UseInMemoryStore();
                    opt.UseDefaultThreadPool(tp =>
                    {
                        tp.MaxConcurrency = 10;
                    });

                    var jobKey = new JobKey("DaemonProcess", "DaemonProcess Group");
                    opt.SchedulerId = "DaemonJob";
                    opt.AddJob<DaemonJob>(jobKey);

                    opt.AddTrigger(opt => opt.ForJob(jobKey)
                    .WithIdentity("DaemonProcess Trigger")
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(30)).RepeatForever())
                    .WithDescription("Daemon Process Job"));
                });
                builder.Services.AddQuartzHostedService(options =>
                {
                    // when shutting down we want jobs to complete gracefully
                    options.WaitForJobsToComplete = true;
                    options.AwaitApplicationStarted = true;
                });
            }
            else
            {
                //DaemonWorker daemonWorker
                builder.Services.AddHostedService<DaemonWorker>();
            }
            
            var host = builder.Build();
            host.Run();
        }
    }
}