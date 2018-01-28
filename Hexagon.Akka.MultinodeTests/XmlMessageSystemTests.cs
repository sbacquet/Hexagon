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
                    akka.loglevel = DEBUG
                    akka.actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                    akka.actor.serialize-messages = off
                    akka.remote.log-remote-lifecycle-events = off
                    akka.cluster.auto-down-unreachable-after = 0s
                    akka.cluster.pub-sub.max-delta-elements = 500
                    akka.test.timefactor = 2
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
                _messageSystem = new XmlMessageSystem(this.Sys, new NodeConfig(from.Name));
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
                            new XmlMessagePattern($@"/request[@routeto = ""{_first.Name}""]"),
                            async (message, sender, self) =>
                            {
                                XmlMessage answer = await _messageSystem.SendMessageAndAwaitResponse(XmlMessage.FromString(@"<question>Why?</question>"));
                                answer.Should().Match<XmlMessage>(mess => mess.Match(@"/answer[. = ""Because.""]"));
                                sender.Tell(XmlMessage.FromString(@"<response>OK</response>"), self);
                            },
                            _first.Name);
                }, _first);

                RunOn(() =>
                {
                    _messageSystem.Register(
                            new XmlMessagePattern(true, $@"/request"),
                            (message, sender, self) =>
                            {
                                sender.Tell(XmlMessage.FromString(@"<response>OK2</response>"), self);
                            },
                            _second.Name);
                    // Actor that reacts to no XmlMessage
                    _messageSystem.Register(
                            new XmlMessagePattern(@"/question[. = ""Why?""]"),
                            (message, sender, self) => sender.Tell(XmlMessage.FromString(@"<answer>Because.</answer>"), self),
                            _second.Name);
                }, _second);
                EnterBarrier("2-registered");

                _messageSystem.Start(5.0 * this.TestKitSettings.TestTimeFactor);
                EnterBarrier("3-started");

                RunOn(() =>
                {
                    string xml = @"<request routeto=""first"">GO</request>";
                    _messageSystem.SendMessage(XmlMessage.FromString(xml), new ActorRefMessageReceiver<XmlMessage>(TestActor));
                    var message1 = ExpectMsg<BytesMessage>();
                    var message2 = ExpectMsg<BytesMessage>();
                    XmlMessage.FromBytes(message1.Bytes).Match(@"/response").Should().BeTrue();
                    XmlMessage.FromBytes(message2.Bytes).Match(@"/response").Should().BeTrue();
                }, _third);

                EnterBarrier("4-done");
            });
        }
    }
}