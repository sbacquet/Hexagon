using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Hexagon;
using Hexagon.AkkaImpl;

namespace Hexagon.AkkaNode
{
    class Options
    {
        [Option('a', "assemblies", Required = true, HelpText = "The assemblies to load, separated by space(s).")]
        public IEnumerable<string> Assemblies { get; set; }
        [Option('c', "config", Required = false, HelpText = "The config file to load.")]
        public string ConfigPath { get; set; }
        [Option('n', "node", Required = true, HelpText = "The node identifier. Must be unique in the cluster.")]
        public string NodeId { get; set; }
        [Option('r', "roles", Required = false, HelpText = "The roles assigned to this node, separated by space(s).")]
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
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<Options>(errs => HandleParseError(errs));
        }

        static void RunOptionsAndReturnExitCode(Options opts)
        {
            Console.WriteLine(opts);
            using (var system = new XmlMessageSystem(new NodeConfig(opts.NodeId, opts.Roles)))
            {
                PatternActionsRegistry<XmlMessage, XmlMessagePattern> registry = new PatternActionsRegistry<XmlMessage, XmlMessagePattern>();
                foreach (var assembly in opts.Assemblies)
                {
                    registry.AddActionsFromAssembly(assembly);
                }
                system.Start(registry);
                registry = null;
                Console.ReadKey(true);
            }
            System.Environment.Exit(0);
        }

        static void HandleParseError(IEnumerable<Error> errors)
        {
            System.Environment.Exit(1);
        }
    }
}
