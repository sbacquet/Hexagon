using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Hexagon.AkkaRest
{
    public class RestRequestConvertersRegistry<M>
    {
        public class Converter
        {
            public Predicate<RestRequest> Match;
            public Func<RestRequest, (M message, bool expectResponse)?> Convert;

            public static Converter FromPOST(Func<JObject, (M message, bool expectResponse)?> convert, params string[] jsonPathConjuncts)
                => new Converter
                {
                    Match = request =>
                    request.Method == RestRequest.EMethod.POST
                    && RestRequest.MatchBody(RestRequest.BodyToJson(request.Body), jsonPathConjuncts),

                    Convert = request => convert(RestRequest.BodyToJson(request.Body))
                };
        }

        public readonly List<Converter> Converters = new List<Converter>();

        public void AddConverter(Converter converter)
            => Converters.Add(converter);

        public (M message, bool expectResponse)? Convert(RestRequest request)
            => GetMatchingConverter(request)?.Convert(request);

        Converter GetMatchingConverter(RestRequest request)
            => Converters.FirstOrDefault(converter => converter.Match(request));

        public void AddConvertersFromAssembly(string assemblyName)
        {
            var assembly = Assembly.Load(assemblyName);
            var registrationMethods = 
                assembly.GetTypes()
                .SelectMany(
                    type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod)
                    .Where(method => method.GetCustomAttributes<RestRequestConvertersRegistrationAttribute>(false).Count() > 0));
            foreach (var method in registrationMethods)
                method.Invoke(null, new[] { this });
        }
    }
}
