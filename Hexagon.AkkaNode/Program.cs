using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Hexagon;
using Hexagon.AkkaImpl;
using System.Threading;

namespace Hexagon.AkkaNode
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

        static void Main(params string[] args)
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
                new AkkaNodeConfig().ToFile<AkkaNodeConfig>(cTemplate);
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
                var config = NodeConfig.FromFile<AkkaNodeConfig>(opts.ConfigPath);
                Console.Title = config.NodeId;
                using (var system = AkkaXmlMessageSystem.Create(config))
                {
                    system.Start(config);
                    Console.WriteLine("Press Control-C to stop.");
                    _quitEvent.WaitOne();
                    //while (Console.ReadKey(true).Key != ConsoleKey.Enter)
                    //{
                    //    try
                    //    {
                    //        var answer = system.SendMessageAndAwaitResponse(XmlMessage.FromString(@"<ping>Ping</ping>"), null);
                    //        Console.WriteLine("Message1 received : {0}", answer?.Content);
                    //    }
                    //    catch (Exception)
                    //    {
                    //        Console.WriteLine("Error : cannot get response");
                    //    }
                    //    try
                    //    {
                    //        var answer2 = system.SendMessageAndAwaitResponse(XmlMessage.FromString(@"<plic>Plic</plic>"), null);
                    //        Console.WriteLine("Message2 received : {0}", answer2?.Content);
                    //    }
                    //    catch (Exception)
                    //    {
                    //        Console.WriteLine("Error : cannot get response");
                    //    }
                    //}
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
    }
}
