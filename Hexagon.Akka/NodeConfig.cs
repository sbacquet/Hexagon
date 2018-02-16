using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.DistributedData;
using Akka.Cluster;
using Akka.Event;

namespace Hexagon.AkkaImpl
{
    public class NodeConfig
    {
        public class ActorProps
        {
            public bool Untrustworthy = false;
            public int MistrustFactor = 1;
            public string RouteOnRole = null;
            public string Router = Constants.DefaultRouter;
            public int TotalMaxRoutees = 1;
            public int MaxRouteesPerNode = 1;
            public bool AllowLocalRoutee = false;
        }
        Dictionary<string, ActorProps> ActorsProps = new Dictionary<string, ActorProps>();
        public readonly string NodeId;
        public readonly double GossipTimeFrameInSeconds;
        public readonly int GossipSynchroAttemptCount;
        public List<string> Roles { get; private set; }
        public List<string> Assemblies { get; private set; }

        public NodeConfig(string nodeId, IEnumerable<string> roles = null, IEnumerable<string> assemblies = null, double gossipTimeFrameInSeconds = 5, int gossipSynchroAttemptCount = 3)
        {
            NodeId = nodeId;
            GossipTimeFrameInSeconds = gossipTimeFrameInSeconds;
            GossipSynchroAttemptCount = gossipSynchroAttemptCount;
            Roles = roles == null ? new List<string>() : roles.ToList();
            Assemblies = assemblies == null ? new List<string>() : assemblies.ToList();
        }

        public static NodeConfig FromFile(string filePath)
        {
            // FAKE
            return new NodeConfig("fake", roles: new[] { "toto" });
        }

        public string GetActorFullName(string actorName)
            => $"{NodeId}_{actorName}";

        public ActorProps GetActorProps(string actorName)
            => ActorsProps.TryGetValue(GetActorFullName(actorName), out ActorProps props) ? props : null;

        public int GetMistrustFactor(string actorName)
            => GetActorProps(actorName)?.MistrustFactor ?? 1;

        public void SetActorProps(string actorName, ActorProps props)
        {
            if (props.Untrustworthy)
                props.MistrustFactor = props.MistrustFactor > 1 ? props.MistrustFactor : 2;
            else
                props.MistrustFactor = 1;
            ActorsProps[GetActorFullName(actorName)] = props;
        }

        public void AddRole(string role)
            => Roles.Add(role);

        public void AddAssembly(string assembly)
            => Assemblies.Add(assembly);

        public void AddThisAssembly()
            => AddAssembly(System.Reflection.Assembly.GetCallingAssembly().GetName().Name);
    }
}
