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
        public PowershellScriptExecutor()
        {
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

                Collection<PSObject> psOutput = powerShellInstance.Invoke();
                return psOutput.Select(output => output.BaseObject);
            }
        }
    }
}
