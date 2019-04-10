using Serilog;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SystemWatcher
{
    public class Watcher
    {
        CancellationTokenSource _cancellation;

        public void Start()
        {
            _cancellation = new CancellationTokenSource();
            CreateLogger();

            Task.Run(async () =>
            {
                var memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

                while (true)
                {
                    if (_cancellation.Token.IsCancellationRequested) break;

                    await Task.Delay(1000);

                    // read cpu
                    Log.Logger.Information("Cpu usage {usage}", cpuCounter.NextValue());

                    // read ram
                    Log.Logger.Information("Ram usage {usage}", memoryCounter.NextValue());
                }
            });
        }

        public void Stop()
        {
            _cancellation.Cancel();
        }

        private static void CreateLogger()
        {
            Log.Logger = new LoggerConfiguration()
                            .Enrich.WithProperty("system", "Miro-Laptop")
                            .Enrich.FromLogContext()
                            .MinimumLevel.Debug()
                            .WriteTo.Console()
                            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
                            {
                                AutoRegisterTemplate = true,
                                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                            })
                            .CreateLogger();
        }
    }
}
