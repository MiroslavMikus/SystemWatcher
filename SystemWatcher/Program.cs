using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace SystemWatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var exitCode = HostFactory.Run(a =>
            {
                a.Service<Watcher>(b =>
                {
                    b.ConstructUsing(c => new Watcher());
                    b.WhenStarted(c => c.Start());
                    b.WhenStopped(c => c.Stop());
                });

                a.RunAsLocalSystem();

                a.SetServiceName("System watcher");
                a.SetDisplayName("System watcher");
            });

            Environment.ExitCode = (int)exitCode;
        }
    }
}
