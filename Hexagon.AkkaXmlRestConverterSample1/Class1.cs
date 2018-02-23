using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hexagon.AkkaRest;

namespace Hexagon.AkkaXmlRestConverterSample1
{
    using XmlConverter = RestRequestConvertersRegistry<XmlMessage>.Converter;

    class Registrar
    {
        [RestRequestConvertersRegistration]
        static void Registration(RestRequestConvertersRegistry<XmlMessage> registry)
        {
            registry.AddConverter(new XmlConverter()
            {
                Match = request => request.Method == RestRequest.EMethod.GET && request.Path == @"/ping",
                Convert = request => (XmlMessage.FromString(@"<ping />"), true)
            });
            registry.AddConverter(XmlConverter.FromPOST(
                bodyJson => (XmlMessage.FromString($@"<ping>{(bodyJson as dynamic).ping}</ping>"), true), // TODO: convert body to xml
                "$.ping"));
            registry.AddConverter(new XmlConverter()
            {
                Match = request => request.Method == RestRequest.EMethod.GET && request.Path == @"/plic",
                Convert = request => (XmlMessage.FromString(@"<plic />"), true)
            });
            registry.AddConverter(XmlConverter.FromPOST(
                bodyJson => (XmlMessage.FromString($@"<plic>{(bodyJson as dynamic).plic}</plic>"), true), // TODO: convert body to xml
                "$.plic"));
        }
    }
}
