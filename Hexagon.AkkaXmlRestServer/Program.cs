using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Hexagon.AkkaImpl;
using System.Threading;
using NHttp;
using System.IO;
using Newtonsoft.Json;
using System.Xml.Linq;
using Hexagon.AkkaRest;

namespace Hexagon.AkkaXmlRestServer
{
    class Options
    {
        [Option('c', "config", Required = false, HelpText = "The config file to load.")]
        public string ConfigPath { get; set; }

        [Option('g', "generateConfig", Required = false, HelpText = "Generate an empty config file template.")]
        public bool GenerateConfig { get; set; }
    }
    class Program
    {
        private static readonly ManualResetEvent _quitEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<Options>(errs => HandleParseError(errs));
        }

        static void RunOptionsAndReturnExitCode(Options opts)
        {
            if (opts.GenerateConfig)
            {
                const string cTemplate = ".\\config_template.xml";
                new Config().ToFile(cTemplate);
                Console.WriteLine(@"Template config file ""{0}"" created.", cTemplate);
                System.Environment.Exit(0);
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                _quitEvent.Set();
                e.Cancel = true;
            };

            try
            {
                var config = Config.FromFile(opts.ConfigPath);
                RestRequestConvertersRegistry<XmlMessage> convertersRegistry = new RestRequestConvertersRegistry<XmlMessage>();
                if (config.Assemblies.Any())
                {
                    foreach (var assembly in config.Assemblies)
                    {
                        convertersRegistry.AddConvertersFromAssembly(assembly);
                    }
                }

                // We don't want to load message handlers in the Rest server
                config.NodeConfig.Assemblies.Clear();

                using (var messageSystem = AkkaXmlMessageSystem.Create(config.NodeConfig))
                using (var restServer = new HttpServer())
                {
                    messageSystem.Start();
                    StartRestServer(
                        messageSystem, 
                        restServer, 
                        config.Port, 
                        TimeSpan.FromSeconds(config.RequestTimeoutInSeconds), 
                        convertersRegistry);

                    Console.WriteLine("Press Control-C to stop.");
                    _quitEvent.WaitOne();
                }
                System.Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error : {0}", ex.Message);
                System.Environment.Exit(1);
            }
        }

        static void HandleParseError(IEnumerable<Error> errors)
        {
            System.Environment.Exit(2);
        }

        static void StartRestServer(
            AkkaMessageSystem<XmlMessage, XmlMessagePattern> system, 
            HttpServer restServer, 
            int port, 
            TimeSpan requestTimeout,
            RestRequestConvertersRegistry<XmlMessage> registry)
        {
            restServer.RequestReceived += ConverterToHttpRequestEventHandler(registry, system, requestTimeout);
            if (port != 0)
                restServer.EndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port);

            restServer.Start();

            Console.WriteLine(@"Rest server started and ready on endpoint : {0}", restServer.EndPoint);
        }

        static RestRequest HttpToRestRequest(HttpRequest httpRequest)
        {
            RestRequest.EMethod method = RestRequest.MethodFromString(httpRequest.HttpMethod);
            string path = httpRequest.Path;
            var query = httpRequest.QueryString;
            string body = null;
            if (method == RestRequest.EMethod.POST)
            {
                using (var reader = new StreamReader(httpRequest.InputStream))
                {
                    body = reader.ReadToEnd();
                }
            }
            return new RestRequest() { Method = method, Path = path, Query = query, Body = body };
        }

        static HttpRequestEventHandler ConverterToHttpRequestEventHandler(
            RestRequestConvertersRegistry<XmlMessage> registry,
            AkkaMessageSystem<XmlMessage, XmlMessagePattern> system,
            TimeSpan requestTimeout)
            => (s, e) =>
            {
                try
                {
                    RestRequest restRequest = HttpToRestRequest(e.Request);
                    var matchingMessage = registry.Convert(restRequest);
                    if (matchingMessage.HasValue)
                    {
                        if (matchingMessage.Value.expectResponse)
                        {
                            var response = system.SendMessageAndAwaitResponse(matchingMessage.Value.message, null, requestTimeout);
                            using (var writer = new StreamWriter(e.Response.OutputStream))
                            {
                                if (response != null)
                                {
                                    string jsonResponse = JsonConvert.SerializeXmlNode(response.AsXml());
                                    writer.Write(jsonResponse);
                                    e.Response.ContentType = "application/json";
                                }
                                else
                                {
                                    writer.Write(@"Cannot route message {0}", matchingMessage.Value.message);
                                    e.Response.ContentType = "text/plain";
                                    e.Response.StatusCode = 412;
                                }
                            }
                        }
                        else
                        {
                            system.SendMessage(matchingMessage.Value.message, null);
                        }
                    }
                    else
                    {
                        using (var writer = new StreamWriter(e.Response.OutputStream))
                        {
                            writer.Write(@"The request does not match any registered handler");
                        }
                        e.Response.ContentType = "text/plain";
                        e.Response.StatusCode = 404;
                    }
                }
                catch (JsonReaderException ex)
                {
                    using (var writer = new StreamWriter(e.Response.OutputStream))
                    {
                        writer.Write(@"JSON is not valid : {0}", ex.Message);
                    }
                    e.Response.ContentType = "text/plain";
                    e.Response.StatusCode = 400;
                }
                catch (Exception ex)
                {
                    using (var writer = new StreamWriter(e.Response.OutputStream))
                    {
                        writer.Write(@"Unknown error : {0}", ex.Message);
                    }
                    e.Response.ContentType = "text/plain";
                    e.Response.StatusCode = 500;
                }
            };
    }
}
