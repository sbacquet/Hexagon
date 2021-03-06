﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hexagon.AkkaImpl;

namespace Hexagon.AkkaNode.Sample2
{
    class RegisterActions
    {
        [PatternActionsRegistration]
        static void Register(PatternActionsRegistry<XmlMessage, XmlMessagePattern> registry)
        {
            registry.AddAction(
                new XmlMessagePattern(@"/plic"),
                (message, sender, self, resource, ms, logger) =>
                {
                    logger.Info(@"Received {0}", message);
                    var xml = message.AsDynamic();
                    sender.Tell(XmlMessage.FromString($@"<ploc>{xml.plic}</ploc>"), self);
                },
                "actor2");
        }
    }
}
