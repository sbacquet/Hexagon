using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;
using Newtonsoft.Json.Linq;

namespace Hexagon
{
    public class JsonMessage : IMessage
    {
        public byte[] Bytes => Encoding.UTF8.GetBytes(Content);
        public string Content { get; private set; }

        private Lazy<JObject> _jObject;
        public JObject AsJObject() => _jObject.Value;

        public T AsObject<T>() => this.AsJObject().ToObject<T>();

        public dynamic AsDynamic() => AsJObject() as dynamic;

        public System.Management.Automation.PSObject AsPSObject<T>()
            => new System.Management.Automation.PSObject(AsObject<T>());

        private JsonMessage()
        {
            _jObject = new Lazy<JObject>(() => JObject.Parse(Content));
        }

        public static JsonMessage FromBytes(byte[] bytes) 
            => new JsonMessage { Content = Encoding.UTF8.GetString(bytes) };

        public static JsonMessage FromString(string xml) 
            => new JsonMessage { Content = xml };

        public static JsonMessage FromObject(object obj, bool withIndentation = false)
            => FromString(
                JObject.FromObject(obj)
                .ToString(withIndentation ? Newtonsoft.Json.Formatting.None : Newtonsoft.Json.Formatting.Indented));

        public override string ToString() => Content;

        public object ToPowershell() => new System.Management.Automation.PSObject(AsJObject()); // TODO
    }
}
