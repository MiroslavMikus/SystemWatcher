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
        private Logger _serviceLogger = new LoggerConfiguration()
                            .MinimumLevel.Verbose()
                            .WriteTo.Console()
                            .WriteTo.File("log/ServiceLog.log")
                            .CreateLogger();

        public void Start()
        {
            _serviceLogger.Information("Starting service");

            _serviceLogger.Information("Settings {ReportIntervallSec}, elastic search url {Url}, using csv logger {csv}",
                Settings.Default.ReportIntervallSec, Settings.Default.ElasticSearchUrl, Settings.Default.UseCSV);

            SettingsValidation();

            CreateLogger();

            _cancellation = new CancellationTokenSource();

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

                    await Task.Delay(Settings.Default.ReportIntervallSec, _cancellation.Token);

                    cpu = Math.Round(cpuCounter.NextValue(), 2);
                    ram = Math.Round(memoryCounter.NextValue(), 2);

                    _elasticLogger?.Information("Cpu {Cpu}, Ram {Ram}", cpu, ram);

                    _csvLogger?.Information("{Cpu},{Memory}", cpu, ram);
                }
            });
        }

        private void SettingsValidation()
        {
            if (Settings.Default.ReportIntervallSec < 1)
            {
                _serviceLogger.Error("Min value of ReportIntervallSec is 1");
                throw new Exception();
            }

            if (!string.IsNullOrEmpty(Settings.Default.ElasticSearchUrl))
            {
                try
                {
                    new Uri(Settings.Default.ElasticSearchUrl);
                }
                catch (Exception ex)
                {
                    _serviceLogger.Error(ex, "Can't parse elastic search url {url}", Settings.Default.ElasticSearchUrl);
                    throw ex;
                }
            }

            if (string.IsNullOrEmpty(Settings.Default.ElasticSearchUrl) && !Settings.Default.UseCSV)
            {
                _serviceLogger.Error("You have to eather specify the serilog url or use csv logger!");
                throw new Exception();
            }
        }

        public void Stop()
        {
            _serviceLogger.Information("Stopping service");
            _cancellation.Cancel();
        }

        private void CreateLogger()
        {
            if (!string.IsNullOrEmpty(Settings.Default.ElasticSearchUrl))
                _elasticLogger = new LoggerConfiguration()
                            .Enrich.WithProperty("system", "SystemWatcher")
                            .MinimumLevel.Information()
                            .WriteTo.Console()
                            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(Settings.Default.ElasticSearchUrl))
                            {
                                AutoRegisterTemplate = true,
                                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                            })
                            .CreateLogger();

            if (Settings.Default.UseCSV)
            {
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
}
