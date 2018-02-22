using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;

namespace Hexagon.AkkaImpl
{
    [XmlRoot("Node")]
    public class AkkaNodeConfig : NodeConfig
    {
        public AkkaNodeConfig() : base()
        {
            Akka = global::Akka.Configuration.Config.Empty;
        }

        public AkkaNodeConfig(string nodeId) : base(nodeId)
        {
            Akka = global::Akka.Configuration.Config.Empty;
        }

        [XmlIgnore]
        public Akka.Configuration.Config Akka;

        [XmlElement("Akka")]
        public XmlCDataSection AkkaConfigCData
        {
            get
            {
                XmlDocument doc = new XmlDocument();
                return doc.CreateCDataSection(Akka.ToString());
            }
            set
            {
                Akka = global::Akka.Configuration.ConfigurationFactory.ParseString(value.Value);
            }
        }
    }
}
