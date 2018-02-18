using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.DistributedData;
using Akka.Cluster;
using Akka.Event;
using System.Xml.Serialization;

namespace Hexagon.AkkaImpl
{
    public class NodeConfig
    {
        public class ActorProps
        {
            public ActorProps() { }
            public ActorProps(string name) { Name = name; }
            [XmlAttribute("Name")]
            public string Name;
            public bool Untrustworthy = false;
            public int MistrustFactor = 1;
            public string RouteOnRole = null;
            public string Router = Constants.DefaultRouter;
            public int TotalMaxRoutees = 1;
            public int MaxRouteesPerNode = 1;
            public bool AllowLocalRoutee = false;
        }
        [XmlIgnore]
        Dictionary<string, ActorProps> ActorsPropsDict;

        public string NodeId;
        public string SystemName;
        [XmlArrayItem("Address")]
        public List<string> SeedNodes { get; private set; }
        [XmlArrayItem("Name")]
        public List<string> Roles { get; private set; }
        [XmlArrayItem("Name")]
        public List<string> Assemblies { get; private set; }
        [XmlArrayItem("Actor")]
        public ActorProps[] Actors
        {
            get => ActorsPropsDict.Values.ToArray();
            set => ActorsPropsDict = value.ToDictionary(actorProps => actorProps.Name);
        }
        public double GossipTimeFrameInSeconds;
        public int GossipSynchroAttemptCount;

        public NodeConfig()
        {
            SystemName = "MessageSystem";
            NodeId = "node1";
            GossipTimeFrameInSeconds = 5;
            GossipSynchroAttemptCount = 3;
            Roles = new List<string>();
            Assemblies = new List<string>();
            ActorsPropsDict = new Dictionary<string, ActorProps>();
            SeedNodes = new List<string>();
        }

        public NodeConfig(string nodeId)
        {
            SystemName = "MessageSystem";
            NodeId = nodeId;
            GossipTimeFrameInSeconds = 5;
            GossipSynchroAttemptCount = 3;
            Roles = new List<string>();
            Assemblies = new List<string>();
            ActorsPropsDict = new Dictionary<string, ActorProps>();
            SeedNodes = new List<string>();
        }

        public static NodeConfig FromFile(string filePath)
        {
            var ser = new System.Xml.Serialization.XmlSerializer(typeof(NodeConfig));
            using (var reader = new System.IO.StreamReader(filePath))
            {
                return (NodeConfig)ser.Deserialize(reader);
            }
        }

        public void ToFile(string filePath)
        {
            var ser = new System.Xml.Serialization.XmlSerializer(typeof(NodeConfig));
            using (var writer = new System.IO.StreamWriter(filePath))
            {
                ser.Serialize(writer, this);
            }
        }

        public string GetActorFullName(string actorName)
            => $"{NodeId}_{actorName}";

        public ActorProps GetActorProps(string actorName)
            => ActorsPropsDict.TryGetValue(GetActorFullName(actorName), out ActorProps props) ? props : null;

        public int GetMistrustFactor(string actorName)
            => GetActorProps(actorName)?.MistrustFactor ?? 1;

        public void SetActorProps(ActorProps props)
        {
            if (props.Untrustworthy)
                props.MistrustFactor = props.MistrustFactor > 1 ? props.MistrustFactor : 2;
            else
                props.MistrustFactor = 1;
            ActorsPropsDict[GetActorFullName(props.Name)] = props;
        }

        public void AddRole(string role)
            => Roles.Add(role);

        public void AddAssembly(string assembly)
            => Assemblies.Add(assembly);

        public void AddThisAssembly()
            => AddAssembly(System.Reflection.Assembly.GetCallingAssembly().GetName().Name);

        public void AddSeedNode(string node)
            => SeedNodes.Add(node);
    }
}
