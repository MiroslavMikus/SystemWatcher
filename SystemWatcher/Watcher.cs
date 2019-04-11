using Serilog;
using Serilog.Core;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SystemWatcher
{
    public class Watcher
    {
        CancellationTokenSource _cancellation;

        private Logger _elasticLogger;
        private Logger _csvLogger;

        private object _ramLock = new object();
        private object _cpuLock = new object();
        private List<double> _cpuValues = new List<double>();
        private List<double> _ramValues = new List<double>();

        public List<double> CpuValues
        {
            get
            {
                lock (_cpuLock)
                    return _cpuValues;
            }
            set
            {
                lock (_cpuLock)
                    _cpuValues = value;
            }
        }

        public List<double> RamValues
        {
            get
            {
                lock (_ramLock)
                    return _ramValues;
            }
            set
            {
                lock (_ramLock)
                    _ramValues = value;
            }
        }

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

                    CpuValues.Add(Math.Round(cpuCounter.NextValue(), 2));
                    RamValues.Add(Math.Round(memoryCounter.NextValue(), 2));
                }
            });

            Task.Run(async () =>
            {
                double cpu, count, ram;

                while (true)
                {
                    await Task.Delay(10_000);

                    if (_cancellation.Token.IsCancellationRequested) break;

                    count = CpuValues.Count;

                    cpu = Math.Round(CpuValues.Average(), 2);
                    CpuValues.Clear();

                    ram = Math.Round(RamValues.Average(), 2);
                    RamValues.Clear();

                    _elasticLogger.Information("Cpu {Cpu}, Ram {Ram}, Count {Count}", cpu, ram, count);

                    _csvLogger.Information("{Cpu},{Memory},{Count}", cpu, ram, count);
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

            _csvLogger = new LoggerConfiguration()
                            .MinimumLevel.Information()
                            .WriteTo.File("log/SystemUsage.csv", outputTemplate: "{Timestamp:dd/MM/yyy},{Timestamp:HH:mm:ss},{Message}{NewLine}")
                            .CreateLogger();

            _csvLogger.Information("TIME,DATE,CPU,RAM,COUNT");
        }
    }
}
