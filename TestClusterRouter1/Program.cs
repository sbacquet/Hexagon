using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration.Hocon;
using Akka.Routing;

namespace TestClusterRouter1
{
    class Program
    {
        static void Main(string[] args)
        {
            var section = (AkkaConfigurationSection)ConfigurationManager.GetSection("akka");
            var config = section.AkkaConfig;
            using (var actorSystem = ActorSystem.Create("ClusterSystem", config))
            {
                var actor = actorSystem.ActorOf(Props.Create(() => new EchoActor.EchoActor()).WithRouter(FromConfig.Instance), "echodispatcher");
                //var actor = actorSystem.ActorOf(FromConfig.Instance.Props(Props.Create<EchoActor.EchoActor>()), "echodispatcher");

                actorSystem.Log.Log(Akka.Event.LogLevel.InfoLevel, "Waiting...");
                System.Threading.Thread.Sleep(5000);
                actorSystem.Log.Log(Akka.Event.LogLevel.InfoLevel, "Done waiting.");

                actor.Tell("coucou 1");
                actor.Tell("coucou 2");
                actor.Tell("coucou 3");
                actor.Tell("coucou 4");
                actor.Tell("coucou 5");
                actor.Tell("coucou 6");
                actor.Tell(new Broadcast("coucou tout le monde !"));
                Console.ReadLine();
                actorSystem.Terminate();
            }
        }
    }
}
