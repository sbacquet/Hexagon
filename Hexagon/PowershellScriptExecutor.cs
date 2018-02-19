using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Collections.ObjectModel;

namespace Hexagon
{
    public class PowershellScriptExecutor
    {
        readonly ILogger _log;

        public PowershellScriptExecutor(ILogger logger)
        {
            _log = logger;
        }

        public IEnumerable<object> Execute(string script, params (string, object)[] parameters)
        {
            using (PowerShell powerShellInstance = PowerShell.Create())
            {
                // use "AddScript" to add the contents of a script file to the end of the execution pipeline.
                // use "AddCommand" to add individual commands/cmdlets to the end of the execution pipeline.
                powerShellInstance.AddScript(script);

                // use "AddParameter" to add a single parameter to the last command/script on the pipeline.
                powerShellInstance.AddParameters(parameters.ToDictionary(nameAndValue => nameAndValue.Item1, nameAndValue => nameAndValue.Item2));

                try
                {
                    Collection<PSObject> psOutput = powerShellInstance.Invoke();
                    if (_log != null)
                    {
                        foreach (var item in powerShellInstance.Streams.Debug)
                        {
                            _log.Debug(item.Message);
                        }
                        foreach (var item in powerShellInstance.Streams.Verbose)
                        {
                            _log.Info(item.Message);
                        }
                        foreach (var item in powerShellInstance.Streams.Information)
                        {
                             _log.Info(item.ToString());
                        }
                        foreach (var item in powerShellInstance.Streams.Warning)
                        {
                            _log.Warning(item.Message);
                        }
                        foreach (var item in powerShellInstance.Streams.Error)
                        {
                            _log.Error(item.ToString());
                        }
                    }
                    return psOutput.Select(output => output.BaseObject);
                }
                catch (Exception ex)
                {
                    Collection<PSObject> output = new Collection<PSObject>();
                    return output;
                }
            }
        }
    }
}
