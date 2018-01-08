using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Configuration.Hocon;
using System.Configuration;
using Akka.Routing;

namespace TestABRouter
{
    public class EchoActor1 : ReceiveActor
    {
        public EchoActor1()
        {
            Receive<string>(message => Console.WriteLine($@"===> {message} to 1"));
        }
    }
    public class EchoActor2 : ReceiveActor
    {
        public EchoActor2()
        {
            Receive<string>(message => Console.WriteLine($@"===> {message} to 2"));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var section = (AkkaConfigurationSection)ConfigurationManager.GetSection("akka");
            var config = section.AkkaConfig;
            using (var system = Akka.Actor.ActorSystem.Create("TestSystem", config))
            {
                var echo1 = system.ActorOf<EchoActor1>("echo1");
                var echo2 = system.ActorOf<EchoActor2>("echo2");
                var paths = new[] { echo1, echo2 }.Select(actorRef => actorRef.Path.ToStringWithoutAddress()).ToList<string>();
                var router = system.ActorOf(Props.Empty.WithRouter(new Finastra.ABRandomGroup(paths, new int[] { 10, 10 })), "router");
                for (int i=0; i<100; ++i)
                    router.Tell("coucou !");

                Console.ReadLine();
                system.Terminate();
            }
        }
    }
}
