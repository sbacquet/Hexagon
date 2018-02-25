using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Hexagon.AkkaImpl;

namespace Hexagon.AkkaXmlRestServer
{
    [XmlRoot("RESTServer")]
    public class Config
    {
        public int Port;
        [XmlArrayItem("Name")]
        public List<string> Assemblies { get; private set; }
        [XmlElement(ElementName = "Node")]
        public AkkaNodeConfig NodeConfig;
        public int RequestTimeoutInSeconds;

        public Config()
        {
            Port = 0;
            Assemblies = new List<string>();
            NodeConfig = new AkkaNodeConfig();
            RequestTimeoutInSeconds = 10;
        }

        public static Config FromFile(string filePath)
        {
            var ser = new System.Xml.Serialization.XmlSerializer(typeof(Config));
            using (var reader = new System.IO.StreamReader(filePath))
            {
                return (Config)ser.Deserialize(reader);
            }
        }

        public void ToFile(string filePath)
        {
            var ser = new System.Xml.Serialization.XmlSerializer(typeof(Config));
            using (var writer = new System.IO.StreamWriter(filePath))
            {
                ser.Serialize(writer, this);
            }
        }
    }
}
