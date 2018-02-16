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
    internal static class PatternUnpublisherActor
    {
        internal class WatchReplicator { internal static WatchReplicator Instance = new WatchReplicator(); }
        internal class IsReady { internal static IsReady Instance = new IsReady(); }
    }
    public class PatternUnpublisherActor<M, P> : ReceiveActor
        where P : IMessagePattern<M>
    {
        private readonly ILoggingAdapter Log;
        private readonly Cluster Cluster;
        private readonly HashSet<UniqueAddress> NodesToWatch;
        private ICancelable _scheduledTask;
        private readonly TimeSpan Delay;

        protected override void PreStart()
        {
            Cluster.Subscribe(Self, typeof(ClusterEvent.MemberRemoved), typeof(ClusterEvent.MemberUp));
            StartOrResumeScheduler();
        }

        protected override void PostStop()
        {
            StopScheduler();
        }

        public PatternUnpublisherActor(ActorDirectory<M, P> actorDirectory, TimeSpan delay)
        {
            Cluster = Cluster.Get(Context.System);
            Log = Logging.GetLogger(Context);
            NodesToWatch = new HashSet<UniqueAddress>(
                this.Cluster.State.Members
                .Where(
                    node => node.UniqueAddress != Cluster.SelfUniqueAddress
                    && node.Status == MemberStatus.Up
                    && node.Roles.Contains(Constants.NodeRoleName))
                .Select(node => node.UniqueAddress));
            Delay = delay;

            ReceiveAsync<ClusterEvent.MemberRemoved>(async mess =>
            {
                var nodeAddress = mess.Member.UniqueAddress;
                NodesToWatch.Remove(nodeAddress);

                if (nodeAddress == Cluster.SelfUniqueAddress || mess.PreviousStatus == MemberStatus.Up)
                    return;

                bool removed = await actorDirectory.RemoveNodeActorsAsync(nodeAddress);
                if (removed)
                    Log.Info("Actors of node {0} properly removed from patterns directory", nodeAddress);
                else
                    Log.Error("Actors of node {0} could not be removed from patterns directory", nodeAddress);
            });

            Receive<ClusterEvent.MemberUp>(mess =>
            {
                NodesToWatch.Add(mess.Member.UniqueAddress);
                StartOrResumeScheduler();
            });

            ReceiveAsync<PatternUnpublisherActor.WatchReplicator>(async _ =>
            {
                if (_scheduledTask == null)
                    return;

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
                        getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, new ReadAll(Delay)));
                        if (getResponse.IsSuccessful)
                        {
                            Log.Info("Data for node {0} are ready (after readall)", node);
                            nodesToRemove.Add(node);
                        }
                    }
                }
                NodesToWatch.ExceptWith(nodesToRemove);
                if (!NodesToWatch.Any())
                    StopScheduler();
            });

            Receive<PatternUnpublisherActor.IsReady>(_ => Context.Sender.Tell(!NodesToWatch.Any()));
        }

        private void StartOrResumeScheduler()
        {
            if (!NodesToWatch.Any())
                return;
            if (_scheduledTask == null)
                _scheduledTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    TimeSpan.Zero,
                    Delay,
                    Self,
                    PatternUnpublisherActor.WatchReplicator.Instance,
                    ActorRefs.NoSender);
        }

        private void StopScheduler()
        {
            if (_scheduledTask != null)
            {
                _scheduledTask.Cancel();
                _scheduledTask = null;
                Log.Info("Scheduler stopped.");
            }
        }
    }
}
