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
            public string Router = "round-robin";
            public int TotalMaxRoutees = 1;
            public int MaxRouteesPerNode = 1;
            public bool AllowLocalRoutee = false;
        }
        Dictionary<string, ActorProps> ActorsProps = new Dictionary<string, ActorProps>();
        public readonly string NodeId;
        public readonly double GossipTimeFrameInSeconds;
        public readonly int GossipSynchroAttemptCount;
        public readonly string[] Roles;

        public NodeConfig(string nodeId, IEnumerable<string> roles = null, double gossipTimeFrameInSeconds = 5, int gossipSynchroAttemptCount = 3)
        {
            NodeId = nodeId;
            GossipTimeFrameInSeconds = gossipTimeFrameInSeconds;
            GossipSynchroAttemptCount = gossipSynchroAttemptCount;
            Roles = roles == null ? new string[] { } : roles.ToArray();
        }

        public static NodeConfig FromFile(string filePath)
        {
            // TODO
            throw new NotImplementedException();
        }

        public string GetFullActorName(string actorName) => $"{NodeId}:{actorName}";

        public ActorProps GetActorProps(string actorName)
        {
            return ActorsProps.TryGetValue(actorName, out ActorProps props) ? props : null;
        }

        public int GetMistrustFactor(string actorName) => GetActorProps(actorName)?.MistrustFactor ?? 1;

        public void SetActorProps(string actorName, ActorProps props)
        {
            if (props.Untrustworthy)
                props.MistrustFactor = props.MistrustFactor > 1 ? props.MistrustFactor : 2;
            else
                props.MistrustFactor = 1;
            ActorsProps[actorName] = props;
        }
    }
}
