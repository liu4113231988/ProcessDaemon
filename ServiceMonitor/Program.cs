using Quartz;
using Serilog;

namespace ServiceMonitor
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
            builder.Services.Configure<List<ServiceModel>>(
                builder.Configuration.GetSection("Services")
            );
            //builder.Services.AddHostedService<Worker>();
            builder.Services.AddWindowsService(option =>
            {
                option.ServiceName = "ServiceMonitor";
            });
            builder.Services.AddLogging();
            builder.Services.AddQuartz(opt =>
            {
                // these are the defaults
                opt.UseSimpleTypeLoader();
                opt.UseInMemoryStore();
                opt.UseDefaultThreadPool(tp =>
                {
                    tp.MaxConcurrency = 10;
                });

                var jobKey = new JobKey("ServiceMonitor", "ServiceMonitor Group");
                opt.SchedulerId = "MonitorJob";
                opt.AddJob<ServiceMonitorJob>(jobKey, (option) => {
                    option.WithDescription("After the monitoring service is stopped, it needs to be started.");
                });

                opt.AddTrigger(opt => opt.ForJob(jobKey)
                .WithIdentity("ServiceMonitor Trigger")
                .StartNow()
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(30)).RepeatForever())
                .WithDescription("Service monitor Job"));
            });
            builder.Services.AddQuartzHostedService(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
                options.AwaitApplicationStarted = true;
            });
            var host = builder.Build();
            host.Run();
        }
    }
}