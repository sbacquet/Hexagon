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
        public struct ActorProps
        {
            public bool Untrustworthy;
            public int MistrustFactor;
        }
        Dictionary<string, ActorProps> ActorsProps = new Dictionary<string, ActorProps>();
        public readonly string NodeId;
        public readonly double GossipTimeFrameInSeconds;
        public readonly int GossipSynchroAttemptCount;

        public NodeConfig(string nodeId, double gossipTimeFrameInSeconds = 5, int gossipSynchroAttemptCount = 3)
        {
            NodeId = nodeId;
            GossipTimeFrameInSeconds = gossipTimeFrameInSeconds;
            GossipSynchroAttemptCount = gossipSynchroAttemptCount;
        }

        public static NodeConfig FromFile(string filePath)
        {
            // TODO
            throw new NotImplementedException();
        }

        public string GetFullActorName(string actorName) => $"{NodeId}:{actorName}";

        public ActorProps? GetActorProps(string actorName)
        {
            return ActorsProps.TryGetValue(actorName, out ActorProps props) ? new Nullable<ActorProps>(props) : null;
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
