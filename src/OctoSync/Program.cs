using System;
using System.Configuration;
using System.IO;
using System.Timers;
using Serilog;
using Topshelf;

namespace OctoSync
{
    class Program
    {
        // ReSharper disable once UnusedParameter.Local
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<Poller>(s =>
                {
                    var log = ConfigureLogger();

                    var interval = double.Parse(
                        ConfigurationManager.AppSettings["PollingInterval"]);

                    var packagePath = ConfigurationManager.AppSettings["PackagePath"];
                    var nugetUrl = ConfigurationManager.AppSettings["NuGetUrl"];
                    var octopusDeployUrl = ConfigurationManager.AppSettings["OctopusDeployUrl"];
                    var octopusDeployApiKey = ConfigurationManager.AppSettings["OctopusDeployApiKey"];

                    var octoSyncer = new OctoSyncer(packagePath, nugetUrl, 
                        octopusDeployUrl, octopusDeployApiKey, log);
                    
                    s.ConstructUsing(name =>
                            new Poller(interval, log, () => octoSyncer.Sync()));
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });

                x.RunAsLocalService();
                x.SetDescription("Sync NuGet packages from a NuGet (MyGet) feed to Octopus Deploy");
                x.SetDisplayName("OctoSync");
                x.SetServiceName("OctoSync");
            });
        }

        private static ILogger ConfigureLogger()
        {
            return new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .Enrich.WithProperty("AppName", "OctoSync")
                .Enrich.WithEnvironmentUserName()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .CreateLogger();
        }
    }

    public class Poller
    {
        private readonly Timer _timer;
        private readonly ILogger _log;
        private readonly object _sync = new object();
        private volatile bool _critical;
        private readonly Action _action;


        public Poller(double interval, ILogger log, Action action)
        {
            _log = log;
            _action = action;
            _timer = new Timer(interval) { AutoReset = true };
            _timer.Elapsed += TimerElapsed;
        }

        public void Start()
        {
            _timer.Start();
            _log.Information("Poller timer started");
        }

        public void Stop()
        {
            _timer.Stop();
            _log.Information("Poller timer stoppd");
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (!_critical)
                {
                    lock (_sync)
                    {
                        if (!_critical)
                        {
                            _critical = true;
                            _log.Information("Poller entering critical section");
                            try
                            {
                                _action();
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, "Oh no! An error occurred and" +
                                               " processing has stopped.");
                                _critical = false;
                                _log.Information("Poller exiting critical section");
                                return;
                            }
                            _critical = false;
                            _log.Information("Poller exiting critical section");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _critical = false;
                _log.Error(ex, "Unhandled exception");
            }
        }
    }
}