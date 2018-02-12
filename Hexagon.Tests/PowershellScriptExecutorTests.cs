using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hexagon.Tests
{
    [TestClass]
    public class PowershellScriptExecutorTests
    {
        [TestMethod]
        public void Execute_PS_script_and_get_result()
        {
            var outputs = new PowershellScriptExecutor().Execute(
                            "param([string]$param1) $param1",
                            ("param1", "ok"));
            Assert.AreEqual("ok", outputs.First());
        }
    }
}
