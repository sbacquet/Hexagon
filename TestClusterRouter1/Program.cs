using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration.Hocon;

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
                var router = actorSystem.ActorOf(Akka.Routing.FromConfig.Instance.Props(Props.Create<EchoActor.EchoActor>()), "echodispatcher");
                router.Tell("coucou");
                Console.ReadLine();
                actorSystem.Terminate();
            }
        }
    }
}
