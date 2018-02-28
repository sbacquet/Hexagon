using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;

namespace Hexagon
{
    public class PSResources : IDisposable
    {
        public Hashtable Resources = null;
        readonly ScriptBlock Destructor;
        readonly ILogger Logger;
        public PSResources(Hashtable resources, ILogger logger, ScriptBlock destructor = null)
        {
            Resources = resources;
            Logger = logger;
            Destructor = destructor;
        }

        public void Dispose()
        {
            if (Destructor != null && Resources != null)
                new PowershellScriptExecutor(Logger).Execute(Destructor.ToString(), ("resources", Resources));
        }
    }
}
