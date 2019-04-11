using Serilog;
using Serilog.Core;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SystemWatcher.Properties;

namespace SystemWatcher
{
    public class Watcher
    {
        CancellationTokenSource _cancellation;

        private Logger _elasticLogger;
        private Logger _csvLogger;

        public void Start()
        {
            if (Settings.Default.ReportIntervallMs < 100)
            {
                throw new ArgumentException("Min value of ReportIntervallMs is 10");
            }

            Console.WriteLine($"Report intervall is {Settings.Default.ReportIntervallMs}");

            _cancellation = new CancellationTokenSource();

            CreateLogger();

            Task.Run(async () =>
            {
                var memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

                double cpu, ram;

                // capture and thrown the first values
                cpu = cpuCounter.NextValue();
                ram = memoryCounter.NextValue();

                while (true)
                {
                    if (_cancellation.Token.IsCancellationRequested) break;

                    await Task.Delay(Settings.Default.ReportIntervallMs, _cancellation.Token);

                    cpu = Math.Round(cpuCounter.NextValue(), 2);
                    ram = Math.Round(memoryCounter.NextValue(), 2);

                    _elasticLogger.Information("Cpu {Cpu}, Ram {Ram}", cpu, ram);

                    _csvLogger.Information("{Cpu},{Memory}", cpu, ram);
                }
            });
        }

        public void Stop()
        {
            _cancellation.Cancel();
        }

        private void CreateLogger()
        {
            _elasticLogger = new LoggerConfiguration()
                            .Enrich.WithProperty("system", "SystemWatcher")
                            .MinimumLevel.Information()
                            .WriteTo.Console()
                            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
                            {
                                AutoRegisterTemplate = true,
                                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                            })
                            .CreateLogger();

            var fileName = $"log/SystemUsage.csv";

            if (!File.Exists(fileName))
                File.AppendAllText(fileName, "DATE,TIME,CPU,MEMORY" + Environment.NewLine);

            _csvLogger = new LoggerConfiguration()
                            .MinimumLevel.Information()
                            .WriteTo.File(fileName, outputTemplate: "{Timestamp:dd/MM/yyy},{Timestamp:HH:mm:ss},{Message}{NewLine}")
                            .CreateLogger();
        }
    }
}
