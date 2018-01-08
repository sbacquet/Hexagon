using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.XPath;
using System.Xml;
using Hexagon;

namespace Hexagon.Tests
{
    [TestClass]
    public class XmlMessageTests
    {
        string xml = @"
<Clients>
  <Client id=""1"">
    <Prenom>Jean-Michel</Prenom>
    <Nom>Laroche</Nom>
    <Telephone>027854632</Telephone>
    <DateInscription>02/06/2007</DateInscription>
    <Status>Activated</Status>
  </Client>
  <Client id=""2"">
    <Prenom>Smith</Prenom>
    <Nom>Cordwainer</Nom>
    <Telephone>024213296</Telephone>
    <DateInscription>12/06/2007</DateInscription>
    <Status>Activated</Status>
  </Client>
  <Client id=""3"">
    <Prenom>Frank</Prenom>
    <Nom>Herbert</Nom>
    <Telephone>022354562</Telephone>
    <DateInscription>08/07/2007</DateInscription>
    <Status>Activated</Status>
  </Client>
  <Client id=""4"">
    <Prenom>Phillipe</Prenom>
    <Nom>Dick</Nom>
    <Telephone>023254789</Telephone>
    <DateInscription>12/07/2007</DateInscription>
    <Status>Deleted</Status>
  </Client>
  <MaxID>4</MaxID>
</Clients>";

        [TestMethod]
        public void XmlMustMatch()
        {
            var message = Hexagon.XmlMessage.FromString(xml);
            var patterns = new XmlMessagePattern(new string[] {
                "//Client[@id='1' and Nom = 'Laroche']",
                "//Client[@id='4' and Status = 'Deleted']",
            });
            Assert.IsTrue(patterns.Match(message));
        }

        [TestMethod]
        public void XmlMustNotMatch()
        {
            var message = Hexagon.XmlMessage.FromString(xml);
            var patterns = new XmlMessagePattern(new string[] {
                "//Client[@id='4' and Status != 'Deleted']"
            });
            Assert.IsFalse(patterns.Match(message));
        }
    }
}
