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
using Akka.Configuration;
using Akka.Remote.TestKit;
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
                ")
                .WithFallback(MultiNodeClusterSpec.ClusterConfig())
                .WithFallback(DistributedData.DefaultConfig())
                .WithFallback(DistributedPubSub.DefaultConfig());

            //TestTransport = true;
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
                var nodeConfig = new NodeConfig(from.Name);
                _messageSystem = new XmlMessageSystem(this.Sys, nodeConfig);
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
                            XmlMessage answer = await _messageSystem.SendMessageAndAwaitResponse(XmlMessage.FromString(@"<question>Why?</question>"), self);
                            answer.Should().Match<XmlMessage>(mess => mess.Match(@"/answer[. = ""Because.""]"));
                            TestActor.Tell("OK");
                        },
                        _first.Name);
                }, _first);

                RunOn(() =>
                {
                    _messageSystem.Register(
                        new XmlMessagePattern(true, "/request"),
                        (message, sender, self) =>
                        {
                            TestActor.Tell("OK");
                        },
                        _second.Name);
                    _messageSystem.Register(
                        new XmlMessagePattern(@"/question[. = ""Why?""]"),
                        (message, sender, self) => sender.Tell(XmlMessage.FromString(@"<answer>Because.</answer>"), self),
                        _second.Name);
                }, _second);

                _messageSystem.Start();
                EnterBarrier("2-started");

                RunOn(() =>
                {
                    string xml = @"<request routeto=""first"">GO</request>";
                    _messageSystem.SendMessage(XmlMessage.FromString(xml), null);
                }, _third);
                RunOn(() => ExpectMsg<string>("OK"), _first);
                RunOn(() => ExpectMsg<string>("OK"), _second);

                EnterBarrier("3-done");
            });
        }
    }
}