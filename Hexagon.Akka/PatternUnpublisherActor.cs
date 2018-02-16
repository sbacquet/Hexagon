using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Event;
using Akka.DistributedData;

namespace Hexagon.AkkaImpl
{
    public class PatternUnpublisherActor<M, P> : ReceiveActor
        where P : IMessagePattern<M>
    {
        readonly ILoggingAdapter Log;
        readonly Cluster Cluster;
        readonly HashSet<UniqueAddress> NodesToWatch;
        readonly ICancelable _scheduledTask;
        const int cDelayInMs = 3000;
        class WatchReplicator { public static WatchReplicator Instance = new WatchReplicator(); }
        public class IsReady { public static IsReady Instance = new IsReady(); }

        protected override void PreStart()
        {
            Cluster.Subscribe(Self, typeof(ClusterEvent.MemberRemoved), typeof(ClusterEvent.MemberUp));
        }

        public PatternUnpublisherActor(ActorDirectory<M, P> actorDirectory)
        {
            Cluster = Cluster.Get(Context.System);
            Log = Logging.GetLogger(Context);
            NodesToWatch = new HashSet<UniqueAddress>(this.Cluster.State.Members.Where(node => node.Status == MemberStatus.Up).Select(node => node.UniqueAddress));
            _scheduledTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(cDelayInMs, cDelayInMs, Self, WatchReplicator.Instance, ActorRefs.NoSender);

            ReceiveAsync<ClusterEvent.MemberRemoved>(async mess =>
            {
                var nodeAddress = mess.Member.UniqueAddress;
                NodesToWatch.Remove(nodeAddress);

                if (nodeAddress == Cluster.SelfUniqueAddress || mess.PreviousStatus == MemberStatus.Up)
                    return;

                bool removed = await actorDirectory.RemoveNodeActors(nodeAddress);
                if (removed)
                    Log.Info("Actors of node {0} properly removed from patterns directory", nodeAddress);
                else
                    Log.Error("Actors of node {0} could not be removed from patterns directory", nodeAddress);
            });

            Receive<ClusterEvent.MemberUp>(mess =>
            {
                NodesToWatch.Add(mess.Member.UniqueAddress);
            });

            ReceiveAsync<WatchReplicator>(async _ =>
            {
                var replicator = DistributedData.Get(Context.System).Replicator;
                HashSet<UniqueAddress> nodesToRemove = new HashSet<UniqueAddress>();
                foreach (var node in NodesToWatch)
                {
                    Log.Info("Looking for data for node {0}...", node);
                    var setKey = new LWWRegisterKey<List<ActorDirectory<M, P>.ActorProps>>(node.ToString());
                    var getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, ReadLocal.Instance));
                    if (getResponse.IsSuccessful)
                    {
                        Log.Info("Data for node {0} are ready", node);
                        nodesToRemove.Add(node);
                    }
                    else
                    {
                        getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, new ReadAll(TimeSpan.FromSeconds(3))));
                        if (getResponse.IsSuccessful)
                        {
                            Log.Info("Data for node {0} are ready (after readall)", node);
                            nodesToRemove.Add(node);
                        }
                    }
                }
                NodesToWatch.RemoveWhere(node => nodesToRemove.Contains(node));
                if (!NodesToWatch.Any())
                    _scheduledTask.Cancel();
            });

            Receive<IsReady>(_ => Context.Sender.Tell(!NodesToWatch.Any()));
        }

    }
}
