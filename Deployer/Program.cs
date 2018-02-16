using Akka.Actor;
using Akka.Configuration;
using Akka.Configuration.Hocon;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Deployer
{
    class Program
    {
        private static readonly ManualResetEvent quitEvent = new ManualResetEvent(false);
        private static readonly ManualResetEvent asTerminatedEvent = new ManualResetEvent(false);

        /// <summary>
        /// NODE 1 PROGRAM
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            int port = args.Length >= 1 ? int.Parse(args[0]) : 0;
            Console.CancelKeyPress += (sender, e) =>
            {
                quitEvent.Set();
                e.Cancel = true;
            };

            var section = (AkkaConfigurationSection)ConfigurationManager.GetSection("akka");
            var config = section.AkkaConfig;
            using (var actorSystem = ActorSystem.Create("ClusterSystem", ConfigurationFactory.ParseString($@"akka.remote.dot-netty.tcp.port = {port}").WithFallback(config)))
            {
                quitEvent.WaitOne();

                Console.WriteLine("Shutting down...");
                var cluster = Akka.Cluster.Cluster.Get(actorSystem);
                cluster.RegisterOnMemberRemoved(() => MemberRemoved(actorSystem));
                cluster.Leave(cluster.SelfAddress);

                asTerminatedEvent.WaitOne();
                Console.WriteLine("Actor system terminated, exiting.");
                if (System.Diagnostics.Debugger.IsAttached) Console.ReadLine();
            }
        }

        private static void MemberRemoved(ActorSystem actorSystem)
        {
            actorSystem.Terminate().Wait();
            asTerminatedEvent.Set();
        }

    }
}
