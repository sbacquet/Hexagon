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
using Hexagon.AkkaImpl;

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
                    akka.test.timefactor = 1
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

        AkkaMessageSystem<XmlMessage, XmlMessagePattern> _messageSystem;
        PatternActionsRegistry<XmlMessage, XmlMessagePattern> _registry;
        AkkaNodeConfig _nodeConfig;

        public XmlMessageSystemTests() : this(new XmlMessageSystemTestsConfig())
        {
            _registry = new PatternActionsRegistry<XmlMessage, XmlMessagePattern>();
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
                _nodeConfig = new AkkaNodeConfig(from.Name);
                _messageSystem = AkkaXmlMessageSystem.Create(this.Sys);
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
                    _registry.AddAsyncAction(
                        new XmlMessagePattern($@"/request[@routeto = ""{_first.Name}""]"),
                        async (message, sender, self, resource, messageSystem, logger) =>
                        {
                            XmlMessage answer = await messageSystem.SendMessageAndAwaitResponseAsync(XmlMessage.FromString(@"<question>Why?</question>"), self);
                            answer.Should().Match<XmlMessage>(mess => mess.Match(@"/answer[. = ""Because.""]"));
                            TestActor.Tell("OK");
                        },
                        _first.Name);
                }, _first);

                RunOn(() =>
                {
                    _registry.AddAction(
                        new XmlMessagePattern(true, @"/request"),
                        (message, sender, self, resource, messageSystem, logger) =>
                        {
                            TestActor.Tell("OK");
                        },
                        _second.Name);
                    _registry.AddPowershellScriptBody(
                        new XmlMessagePattern(@"/question[. = ""Why?""]"),
                        @"'<answer>Because.</answer>'",
                        _second.Name
                        );
                }, _second);

                _messageSystem.Start(_nodeConfig, _registry);
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