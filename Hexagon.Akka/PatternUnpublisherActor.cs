using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Event;

namespace Hexagon.AkkaImpl
{
    public class PatternUnpublisherActor<M, P> : ReceiveActor
        where P : IMessagePattern<M>
    {
        readonly ILoggingAdapter Log;
        readonly Cluster Cluster;

        protected override void PreStart()
        {
            Cluster.Subscribe(Self, typeof(ClusterEvent.MemberRemoved));
        }

        public PatternUnpublisherActor(ActorDirectory<M, P> actorDirectory)
        {
            Cluster = Cluster.Get(Context.System);
            Log = Logging.GetLogger(Context);

            ReceiveAsync<ClusterEvent.MemberRemoved>(async mess =>
            {
                var nodeAddress = mess.Member.UniqueAddress;
                if (nodeAddress == Cluster.SelfUniqueAddress || mess.PreviousStatus == MemberStatus.Up)
                    return;

                bool removed = await actorDirectory.RemoveNodeActors(nodeAddress);
                if (removed)
                    Log.Info("Actors of node {0} properly removed from patterns directory", nodeAddress);
                else
                    Log.Error("Actors of node {0} could not be removed from patterns directory", nodeAddress);
            });
        }

    }
}
