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
                Convert = request => (XmlMessage.FromString(@"<ping></ping>"), true)
            });
            registry.AddConverter(XmlConverter.FromPOST(
                bodyJson => (XmlMessage.FromString(@"<ping></ping>"), true),
                "$.ping"));
            registry.AddConverter(new XmlConverter()
            {
                Match = request => request.Method == RestRequest.EMethod.GET && request.Path == @"/plic",
                Convert = request => (XmlMessage.FromString(@"<plic></plic>"), true)
            });
            registry.AddConverter(XmlConverter.FromPOST(
                bodyJson => (XmlMessage.FromString(@"<plic></plic>"), true),
                "$.plic"));
        }
    }
}
