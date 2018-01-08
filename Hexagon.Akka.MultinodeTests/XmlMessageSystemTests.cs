//-----------------------------------------------------------------------
// <copyright file="DistributedPubSubMediatorSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Cluster.TestKit;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Cluster.Tools.PublishSubscribe.Internal;
using Akka.Configuration;
using Akka.Event;
using Akka.Remote.TestKit;
using Akka.TestKit;
using Xunit;
using FluentAssertions;

namespace Hexagon.AkkaImpl.MultinodeTests
{
    public class XmlMessageSystemTestsConfig : MultiNodeConfig
    {
        public readonly RoleName First;
        public readonly RoleName Second;
        public readonly RoleName Third;

        public XmlMessageSystemTestsConfig()
        {
            First = Role("first");
            Second = Role("second");
            Third = Role("third");

            CommonConfig = ConfigurationFactory.ParseString(@"
                akka.loglevel = INFO
                akka.actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                akka.actor.serialize-messages = off
                akka.remote.log-remote-lifecycle-events = off
                akka.cluster.auto-down-unreachable-after = 0s
                akka.cluster.pub-sub.max-delta-elements = 500
            ").WithFallback(DistributedPubSub.DefaultConfig());
        }
    }

    public class XmlMessageSystemTests : MultiNodeClusterSpec
    {
        #region setup 
        private readonly RoleName _first;
        private readonly RoleName _second;
        private readonly RoleName _third;

        Hexagon.AkkaImpl.XmlMessageSystem _messageSystem;

        public XmlMessageSystemTests() : this(new XmlMessageSystemTestsConfig())
        {
        }

        protected XmlMessageSystemTests(XmlMessageSystemTestsConfig config) : base(config, typeof(XmlMessageSystemTests))
        {
            _first = config.First;
            _second = config.Second;
            _third = config.Third;
        }

        public IActorRef Mediator { get { return DistributedPubSub.Get(Sys).Mediator; } }

        private void Join(RoleName from, RoleName to)
        {
            RunOn(() =>
            {
                Cluster.Join(Node(to).Address);
                _messageSystem = new XmlMessageSystem(this.Sys);
            }, from);
            EnterBarrier(from.Name + "-joined");
        }
        #endregion

        [MultiNodeFact]
        public void Tests()
        {
            Must_startup_3_nodes_cluster();
            Must_receive_message_by_path();
        }

        public void Must_startup_3_nodes_cluster()
        {
            Within(TimeSpan.FromSeconds(15), () =>
            {
                Join(_first, _first);
                Join(_second, _first);
                Join(_third, _first);
                EnterBarrier("after-1");
            });
        }

        public void Must_receive_message_by_path()
        {
            Within(TimeSpan.FromSeconds(15), () =>
            {
                RunOn(() =>
                {
                    var a1 = Tuple.Create<Action<XmlMessage, ICanReceiveMessage<XmlMessage>, ICanReceiveMessage<XmlMessage>>, Predicate<XmlMessage>>(
                        (message, sender, self) => sender.Tell(XmlMessage.FromString("<message>OK!</message>"), self),
                        m => true);
                    var messageFactory = new XmlMessageFactory();
                    var actor1 = Sys.ActorOf(Props.Create(() => new Actor<XmlMessage, XmlMessagePattern>(
                        new[] { a1 },
                        messageFactory)), "actor1");

                    Mediator.Tell(new Put(actor1));

                    System.Threading.Thread.Sleep(3000);

                }, _first);

                EnterBarrier("2-registered");

                RunOn(() =>
                {
                    // send to actor at the same node
                    Mediator.Tell(new Send("/user/actor1", XmlMessage.FromString("<message>OK?</message>")));
                    ExpectMsg<BytesMessage>();
                }, _second, _third);

                EnterBarrier("after-2");
            });
        }
    }
}