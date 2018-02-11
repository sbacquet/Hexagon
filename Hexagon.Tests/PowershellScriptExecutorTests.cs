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
            var output = new PowershellScriptExecutor().Execute(
                            "param($message, $sender, $self, $messageSystem) " + "$message",
                            ("message", XmlMessage.FromString("<ok></ok>")),
                            ("sender", "sender"),
                            ("self", "self"),
                            ("messageSystem", "messageSystem"));
        }
    }
}
