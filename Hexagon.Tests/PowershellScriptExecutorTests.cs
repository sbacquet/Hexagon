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
        public void Test1()
        {
            var ps = new PowershellScriptExecutor();
            string script = @"param($param1) ""param1 = $param1""";
            var outputs = ps.Execute(script, ("param1", 3));
            Assert.AreEqual("param1 = 3", outputs.First());
        }
    }
}
