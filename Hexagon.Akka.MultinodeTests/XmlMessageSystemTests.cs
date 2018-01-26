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
using Akka.DistributedData;

namespace Hexagon.AkkaImpl.MultinodeTests
{
    using XmlActor = Actor<XmlMessage, XmlMessagePattern>;

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

            CommonConfig = 
                ConfigurationFactory
                .ParseString(@"
                    akka.loglevel = INFO
                    akka.actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                    akka.actor.serialize-messages = off
                    akka.remote.log-remote-lifecycle-events = off
                    akka.cluster.auto-down-unreachable-after = 0s
                    akka.cluster.pub-sub.max-delta-elements = 500
                ")
                .WithFallback(DistributedData.DefaultConfig())
                .WithFallback(DistributedPubSub.DefaultConfig());

            TestTransport = true;
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
            XmlMessageSystem_must_work();
        }

        void Must_startup_3_nodes_cluster()
        {
            Within(TimeSpan.FromSeconds(15), () =>
            {
                Join(_first, _first);
                Join(_second, _first);
                Join(_third, _first);
            });
        }

        void XmlMessageSystem_must_work()
        {
            Within(TimeSpan.FromSeconds(30), () =>
            {
                RunOn(() =>
                {
                    _messageSystem.RegisterAsync(
                            new XmlMessagePattern(@"/request"),
                            async (message, sender, self) =>
                            {
                                XmlMessage answer = await _messageSystem.SendMessageAndAwaitResponse(XmlMessage.FromString(@"<question>Why?</question>"));
                                answer.Should().Match<XmlMessage>(mess => mess.Match(@"/answer[. = ""Because.""]"));
                                sender.Tell(XmlMessage.FromString(@"<response>OK</response>"), self);
                            },
                            "actor1");
                }, _first);

                RunOn(() =>
                {
                    // Actor that reacts to no XmlMessage
                    _messageSystem.Register(
                            new XmlMessagePattern(@"/question[. = ""Why?""]"),
                            (message, sender, self) => sender.Tell(XmlMessage.FromString(@"<answer>Because.</answer>"), self),
                            "actor3");
                }, _third);
                EnterBarrier("2-registered");

                _messageSystem.Start();
                EnterBarrier("3-started");

                RunOn(() =>
                {
                    string xml = @"<request>GO</request>";
                    _messageSystem.SendMessage(XmlMessage.FromString(xml), new ActorRefMessageReceiver<XmlMessage>(TestActor));
                    ExpectMsg<BytesMessage>(message => XmlMessage.FromBytes(message.Bytes).Match(@"/response[. = ""OK""]"));
                }, _second);

                EnterBarrier("4-done");
            });
        }
    }
}