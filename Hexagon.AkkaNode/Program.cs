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

        [Option('n', "notInteractive", Required = false, HelpText = "Set to true when the console host is not available (run as a service)")]
        public bool NotInteractive { get; set; }
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
                if (!opts.NotInteractive)
                    Console.Title = config.NodeId;
                using (var system = AkkaXmlMessageSystem.Create(config))
                {
                    system.Start(config);
                    if (!opts.NotInteractive)
                        Console.WriteLine("Press Control-C to exit, Enter to clear screen.");
                    if (opts.NotInteractive)
                    {
                        _quitEvent.WaitOne();
                    }
                    else
                    {
                        bool exit = false;
                        do
                        {
                            if (Console.KeyAvailable)
                            {
                                var key = Console.ReadKey(true);
                                if (key.Key == ConsoleKey.Enter)
                                    Console.Clear();
                            }
                            exit = _quitEvent.WaitOne(100);
                        } while (!exit);
                    }
                }
                System.Environment.Exit(0);
            }
            catch (Exception ex)
            {
                if (!opts.NotInteractive)
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
