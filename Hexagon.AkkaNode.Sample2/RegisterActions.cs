using System;
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
                (message, sender, self, ms) => sender.Tell(XmlMessage.FromString($@"<ploc>{self.Path}</ploc>"), self),
                "actor2");
        }
    }
}
