using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Hexagon;
using System.Xml;

namespace Hexagon.Automation.Cmdlets
{
    [Cmdlet(VerbsCommunications.Send, "XmlMessage")]
    public class SendXmlMessage : PSCmdlet
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public MessageSystem<XmlMessage, XmlMessagePattern> System { get; set; }

        [Parameter(Mandatory = true)]
        public XmlDocument Message { get; set; }

        [Parameter(Mandatory = false)]
        public ICanReceiveMessage<XmlMessage> Sender { get; set; }

        protected override void EndProcessing()
        {
            System.SendMessage(XmlMessage.FromXml(Message), Sender);
        }
    }

    [Cmdlet(VerbsCommunications.Send, "XmlMessageAndAwaitResponse")]
    public class SendXmlMessageAndAwaitResponse : PSCmdlet
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public MessageSystem<XmlMessage, XmlMessagePattern> System { get; set; }

        [Parameter(Mandatory = true)]
        public XmlDocument Message { get; set; }

        [Parameter(Mandatory = false)]
        public int TimeoutInSeconds { get; set; } = 5;

        protected override void EndProcessing()
        {
            XmlMessage response = System.SendMessageAndAwaitResponse(XmlMessage.FromXml(Message), null, TimeSpan.FromSeconds(TimeoutInSeconds));
            if (response != null)
                WriteObject(response.AsXml());
        }
    }
}
