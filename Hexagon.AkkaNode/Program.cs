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
        [Option('a', "assemblies", Required = false, HelpText = "The assemblies to load, separated by space(s). Mandatory if no role specified.")]
        public IEnumerable<string> Assemblies { get; set; }
        [Option('c', "config", Required = false, HelpText = "The config file to load.")]
        public string ConfigPath { get; set; }
        [Option('n', "node", Required = true, HelpText = "The node identifier (must be unique in the cluster).")]
        public string NodeId { get; set; }
        [Option('r', "roles", Required = false, HelpText = "The roles assigned to this node, separated by space(s). Mandatory if no assembly specified.")]
        public IEnumerable<string> Roles { get; set; }

        public override string ToString()
        {
            return $@"
Assemblies: {string.Join(", ", Assemblies)}
Config: {ConfigPath}
Node id: {NodeId}
Roles: {string.Join(", ", Roles)}
";
        }
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
            Console.WriteLine(opts);
            Console.CancelKeyPress += (sender, e) =>
            {
                _quitEvent.Set();
                e.Cancel = true;
            };

            if (!opts.Roles.Any() && !opts.Assemblies.Any())
            {
                Console.WriteLine("ERROR: Either an assembly or a role must be specified.\n");
                Main("--help");
            }
            using (var system = new XmlMessageSystem(new NodeConfig(opts.NodeId, opts.Roles, opts.Assemblies)))
            {
                system.Start();
                Console.WriteLine("Press Control-C to stop.");
                //_quitEvent.WaitOne();
                while (Console.ReadKey(true).Key != ConsoleKey.Enter)
                {
                    try
                    {
                        var answer = system.SendMessageAndAwaitResponse(XmlMessage.FromString(@"<ping>Ping</ping>"), null);
                        Console.WriteLine("Message1 received : {0}", answer?.Content);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Error : cannot get response");
                    }
                    try
                    {
                        var answer2 = system.SendMessageAndAwaitResponse(XmlMessage.FromString(@"<plic>Plic</plic>"), null);
                        Console.WriteLine("Message2 received : {0}", answer2?.Content);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Error : cannot get response");
                    }
                }
            }
            System.Environment.Exit(0);
        }

        static void HandleParseError(IEnumerable<Error> errors)
        {
            System.Environment.Exit(1);
        }
    }
}
