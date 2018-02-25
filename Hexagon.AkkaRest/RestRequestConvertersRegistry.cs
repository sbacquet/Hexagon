using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Hexagon.AkkaRest
{
    public class RestRequestConvertersRegistry<M>
    {
        public class Converter
        {
            public Predicate<RestRequest> Match;
            public Func<RestRequest, (M message, bool expectResponse)?> ConvertFromRequest;
            public Func<M, JObject> ConvertToResponse;

            public static Converter FromPOST(string pathRegex, Func<string[], JObject, M> convertFromPathAndBody, bool expectResponse, Func<M, JObject> convertToResponse, params string[] jsonPathConjuncts)
                => new Converter
                {
                    Match = request =>
                    request.Method == RestRequest.EMethod.POST
                    && Regex.IsMatch(request.Path, pathRegex)
                    && RestRequest.MatchBody(RestRequest.BodyToJson(request.Body), jsonPathConjuncts),

                    ConvertFromRequest = request => (convertFromPathAndBody(request.Path.Substring(1).Split('/'), RestRequest.BodyToJson(request.Body)), expectResponse),

                    ConvertToResponse = convertToResponse
                };

            public static Converter FromGET(string pathRegex, Func<string[], NameValueCollection, M> convertFromPathAndQuery, Func<M, JObject> convertToResponse)
                => new Converter
                {
                    Match = request => request.Method == RestRequest.EMethod.GET && Regex.IsMatch(request.Path, pathRegex),

                    ConvertFromRequest = request => (convertFromPathAndQuery(request.Path.Substring(1).Split('/'), request.Query), true),

                    ConvertToResponse = convertToResponse
                };
        }

        public readonly List<Converter> Converters = new List<Converter>();

        public void AddConverter(Converter converter)
            => Converters.Add(converter);

        public Converter GetMatchingConverter(RestRequest request)
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
